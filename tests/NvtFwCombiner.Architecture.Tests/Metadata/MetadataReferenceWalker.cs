using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace NvtFwCombiner.Architecture.Tests.Metadata;

internal sealed record MetadataReference(string Owner, string Member, string Target, string TargetMember = "");

// Reads PE data only. No Assembly.Load, reflection invocation, source readers or production execution.
internal sealed class MetadataReferenceWalker
{
    private readonly PEReader _pe;
    private readonly MetadataReader _reader;
    private readonly Names _names;
    private readonly Names _silent;
    private readonly HashSet<MetadataReference> _references = [];
    private readonly Dictionary<string, (string Owner, string Member)> _stateMachines = new(StringComparer.Ordinal);
    private string _owner = "";
    private string _member = "";

    private MetadataReferenceWalker(PEReader pe, string? directory)
    {
        _pe = pe;
        _reader = pe.GetMetadataReader();
        _names = new Names(_reader, name => _references.Add(new(_owner, _member, name)), directory);
        _silent = new Names(_reader, _ => { }, directory);
    }
    internal static ImmutableArray<MetadataReference> Read(ImmutableArray<byte> image, string? directory = null)
    {
        using var pe = new PEReader(image);
        return new MetadataReferenceWalker(pe, directory).Walk();
    }
    private string MethodName(MethodDefinitionHandle handle)
    {
        MethodDefinition method = _reader.GetMethodDefinition(handle);
        MethodSignature<string> signature = method.DecodeSignature(_silent, null);
        return $"{_reader.GetString(method.Name)}({string.Join(",", signature.ParameterTypes)})";
    }
    private ImmutableArray<MetadataReference> Walk()
    {
        // Use the actual Async/IteratorStateMachineAttribute link, not a generated ordinal.
        foreach (TypeDefinitionHandle type in _reader.TypeDefinitions)
        {
            foreach (MethodDefinitionHandle handle in _reader.GetTypeDefinition(type).GetMethods())
            {
                foreach (CustomAttributeHandle attribute in _reader.GetMethodDefinition(handle).GetCustomAttributes())
                {
                    CustomAttribute data = _reader.GetCustomAttribute(attribute);
                    string name = ConstructorType(data.Constructor);
                    if (name is "System.Runtime.CompilerServices.AsyncStateMachineAttribute" or "System.Runtime.CompilerServices.IteratorStateMachineAttribute" or "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute")
                    {
                        string generated = (string)data.DecodeValue(_silent).FixedArguments[0].Value!;
                        _stateMachines.Add(generated, (LogicalOwner(type).Owner, LogicalMethod(handle)));
                    }
                }
            }
        }
        foreach (TypeDefinitionHandle handle in _reader.TypeDefinitions)
        {
            TypeDefinition type = _reader.GetTypeDefinition(handle);
            string name = _silent.Type(handle);
            (_owner, _member) = LogicalOwner(handle);
            string typeMember = _member.Length == 0 ? "<type>" : _member;
            _member = typeMember;
            Token(type.BaseType);
            Attributes(type.GetCustomAttributes());
            Constraints(type.GetGenericParameters());
            foreach (InterfaceImplementationHandle contract in type.GetInterfaceImplementations())
            {
                InterfaceImplementation item = _reader.GetInterfaceImplementation(contract);
                Token(item.Interface);
                Attributes(item.GetCustomAttributes());
            }
            foreach (FieldDefinitionHandle field in type.GetFields())
            {
                _member = typeMember == "<type>" ? _reader.GetString(_reader.GetFieldDefinition(field).Name) : typeMember;
                _ = _reader.GetFieldDefinition(field).DecodeSignature(_names, null);
                Attributes(_reader.GetFieldDefinition(field).GetCustomAttributes());
            }
            foreach (PropertyDefinitionHandle property in type.GetProperties())
            {
                _member = _reader.GetString(_reader.GetPropertyDefinition(property).Name);
                _ = _reader.GetPropertyDefinition(property).DecodeSignature(_names, null);
                Attributes(_reader.GetPropertyDefinition(property).GetCustomAttributes());
            }
            foreach (EventDefinitionHandle eventHandle in type.GetEvents())
            {
                EventDefinition item = _reader.GetEventDefinition(eventHandle);
                _member = _reader.GetString(item.Name);
                Token(item.Type);
                Attributes(item.GetCustomAttributes());
            }
            foreach (MethodDefinitionHandle method in type.GetMethods())
            {
                _member = _stateMachines.TryGetValue(name, out var logical) ? logical.Member : LogicalMethod(method);
                MethodDefinition item = _reader.GetMethodDefinition(method);
                _ = item.DecodeSignature(_names, null);
                Attributes(item.GetCustomAttributes());
                Constraints(item.GetGenericParameters());
                foreach (ParameterHandle parameter in item.GetParameters()) { Attributes(_reader.GetParameter(parameter).GetCustomAttributes()); }
                if (item.RelativeVirtualAddress == 0) { continue; }
                MethodBodyBlock body = _pe.GetMethodBody(item.RelativeVirtualAddress);
                if (!body.LocalSignature.IsNil) { _ = _reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(_names, null); }
                foreach (ExceptionRegion region in body.ExceptionRegions) { Token(region.CatchType); }
                WalkIl(body.GetILReader());
            }
        }
        return [.. _references.OrderBy(item => item.Owner, StringComparer.Ordinal).ThenBy(item => item.Member, StringComparer.Ordinal)
            .ThenBy(item => item.Target, StringComparer.Ordinal).ThenBy(item => item.TargetMember, StringComparer.Ordinal)];
    }
    private (string Owner, string Member) LogicalOwner(TypeDefinitionHandle handle)
    {
        string name = _silent.Type(handle);
        if (_stateMachines.TryGetValue(name, out var logical)) { return logical; }
        TypeDefinition type = _reader.GetTypeDefinition(handle);
        string simple = _reader.GetString(type.Name);
        return !type.GetDeclaringType().IsNil && simple.StartsWith('<') ? LogicalOwner(type.GetDeclaringType()) : (name, "");
    }
    private string LogicalMethod(MethodDefinitionHandle handle)
    {
        string name = _reader.GetString(_reader.GetMethodDefinition(handle).Name);
        // Lambdas and local functions in display classes belong to the enclosing method.
        if (name.StartsWith('<') && name.IndexOf('>', StringComparison.Ordinal) is > 1 and var end)
        {
            return name[1..end] + " [closure]";
        }
        return MethodName(handle);
    }
    private string ConstructorType(EntityHandle handle)
    {
        return handle.Kind == HandleKind.MemberReference ? _silent.Type(_reader.GetMemberReference((MemberReferenceHandle)handle).Parent)
            : _silent.Type(_reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType());
    }
    private void Attributes(CustomAttributeHandleCollection attributes)
    {
        foreach (CustomAttributeHandle handle in attributes)
        {
            CustomAttribute attribute = _reader.GetCustomAttribute(handle);
            Token(attribute.Constructor);
            CustomAttributeValue<string> value = attribute.DecodeValue(_names);
            foreach (CustomAttributeTypedArgument<string> argument in value.FixedArguments) { AttributeValue(argument.Type, argument.Value); }
            foreach (CustomAttributeNamedArgument<string> argument in value.NamedArguments) { AttributeValue(argument.Type, argument.Value); }
        }
    }
    private void AttributeValue(string type, object? value)
    {
        if (type == "System.Type" && value is string name) { _ = _references.Add(new(_owner, _member, name)); }
        if (value is ImmutableArray<CustomAttributeTypedArgument<string>> items)
        {
            foreach (CustomAttributeTypedArgument<string> item in items) { AttributeValue(item.Type, item.Value); }
        }
    }
    private void Constraints(GenericParameterHandleCollection parameters)
    {
        foreach (GenericParameterHandle handle in parameters)
        {
            GenericParameter parameter = _reader.GetGenericParameter(handle);
            Attributes(parameter.GetCustomAttributes());
            foreach (GenericParameterConstraintHandle constraint in parameter.GetConstraints())
            {
                GenericParameterConstraint item = _reader.GetGenericParameterConstraint(constraint);
                Token(item.Type);
                Attributes(item.GetCustomAttributes());
            }
        }
    }
    private void Token(EntityHandle handle)
    {
        if (handle.IsNil) { return; }
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
            case HandleKind.TypeReference:
            case HandleKind.TypeSpecification: _ = _names.Type(handle); break;
            case HandleKind.MemberReference:
                MemberReference member = _reader.GetMemberReference((MemberReferenceHandle)handle);
                string parent = _names.Type(member.Parent);
                _ = _references.Add(new(_owner, _member, parent, _reader.GetString(member.Name)));
                if (member.GetKind() == MemberReferenceKind.Field) { _ = member.DecodeFieldSignature(_names, null); }
                else { _ = member.DecodeMethodSignature(_names, null); }
                break;
            case HandleKind.MethodSpecification:
                MethodSpecification method = _reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                Token(method.Method);
                _ = method.DecodeSignature(_names, null);
                break;
            case HandleKind.MethodDefinition:
                MethodDefinition definition = _reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                _ = _references.Add(new(_owner, _member, _names.Type(definition.GetDeclaringType()), _reader.GetString(definition.Name)));
                _ = definition.DecodeSignature(_names, null);
                break;
            case HandleKind.FieldDefinition:
                FieldDefinition field = _reader.GetFieldDefinition((FieldDefinitionHandle)handle);
                _ = _references.Add(new(_owner, _member, _names.Type(field.GetDeclaringType()), _reader.GetString(field.Name)));
                _ = field.DecodeSignature(_names, null);
                break;
            case HandleKind.StandaloneSignature: _ = _reader.GetStandaloneSignature((StandaloneSignatureHandle)handle).DecodeMethodSignature(_names, null); break;
            default: throw new BadImageFormatException($"Metadata.Token: unsupported {handle.Kind}");
        }
    }
    private void WalkIl(BlobReader il)
    {
        while (il.RemainingBytes > 0)
        {
            int op = il.ReadByte();
            if (op == 0xfe) { op = 0xfe00 | il.ReadByte(); }
            // ECMA-335 III operand encodings. Prefixes, calli and ldtoken are included.
            if (op is 0x27 or 0x28 or 0x29 or 0x6f or 0x70 or 0x71 or 0x73 or 0x74 or 0x75 or 0x79
                or >= 0x7b and <= 0x81 or 0x8c or 0x8d or 0x8f or 0xa3 or 0xa4 or 0xa5 or 0xc2 or 0xc6 or 0xd0
                or 0xfe06 or 0xfe07 or 0xfe15 or 0xfe16 or 0xfe1c) { Token(MetadataTokens.EntityHandle(il.ReadInt32())); }
            else if (op == 0x45) { int count = il.ReadInt32(); il.Offset = checked(il.Offset + checked(count * 4)); }
            else
            {
                int size = op switch
                {
                    0x20 or 0x22 or >= 0x38 and <= 0x44 or 0x72 or 0xdd => 4,
                    0x21 or 0x23 => 8,
                    >= 0x0e and <= 0x13 or 0x1f or >= 0x2b and <= 0x37 or 0xde or 0xfe12 or 0xfe19 => 1,
                    >= 0xfe09 and <= 0xfe0e => 2,
                    _ => 0,
                };
                il.Offset += size;
            }
        }
    }
    private sealed class Names(MetadataReader reader, Action<string> seen, string? directory) : ISignatureTypeProvider<string, object?>, ICustomAttributeTypeProvider<string>
    {
        public string Type(EntityHandle handle)
        {
            return handle.Kind switch
            {
                HandleKind.TypeDefinition => GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0),
                HandleKind.TypeReference => GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0),
                HandleKind.TypeSpecification => GetTypeFromSpecification(reader, null, (TypeSpecificationHandle)handle, 0),
                HandleKind.MethodDefinition => Type(reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType()),
                HandleKind.ModuleReference => reader.GetString(reader.GetModuleReference((ModuleReferenceHandle)handle).Name),
                _ => throw new BadImageFormatException($"Metadata.Type: unsupported {handle.Kind}"),
            };
        }
        public string GetTypeFromDefinition(MetadataReader metadata, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = type.GetDeclaringType().IsNil ? Qualify(metadata.GetString(type.Namespace), metadata.GetString(type.Name))
                : Type(type.GetDeclaringType()) + "+" + metadata.GetString(type.Name);
            seen(name);
            return name;
        }
        public string GetTypeFromReference(MetadataReader metadata, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference type = metadata.GetTypeReference(handle);
            string name = type.ResolutionScope.Kind == HandleKind.TypeReference ? Type(type.ResolutionScope) + "+" + metadata.GetString(type.Name)
                : Qualify(metadata.GetString(type.Namespace), metadata.GetString(type.Name));
            seen(name);
            return name;
        }
        private static string Qualify(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
        public string GetTypeFromSpecification(MetadataReader metadata, object? context, TypeSpecificationHandle handle, byte rawTypeKind) => metadata.GetTypeSpecification(handle).DecodeSignature(this, context);
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> arguments) => genericType + "<" + string.Join(",", arguments) + ">";
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPinnedType(string elementType) => elementType;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fn(" + string.Join(",", signature.ParameterTypes) + ")->" + signature.ReturnType;
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;
        public string GetSystemType() => "System.Type";
        public bool IsSystemType(string type) => type == GetSystemType();
        public string GetTypeFromSerializedName(string name)
        {
            // A type-valued attribute can serialize a constructed generic; emit each named argument too.
            string simple = name.Split(',', '[')[0].Trim();
            seen(simple);
            foreach (string part in name.Split('[', ']'))
            {
                string candidate = part.Split(',')[0].Trim();
                if (candidate.Contains('.', StringComparison.Ordinal)) { seen(candidate); }
            }
            return simple;
        }
        public PrimitiveTypeCode GetUnderlyingEnumType(string type)
        {
            TypeDefinitionHandle handle = reader.TypeDefinitions.FirstOrDefault(item => TypeName(item) == type);
            if (!handle.IsNil)
            {
                FieldDefinitionHandle field = reader.GetTypeDefinition(handle).GetFields().Single(item => reader.GetString(reader.GetFieldDefinition(item).Name) == "value__");
                return Enum.Parse<PrimitiveTypeCode>(reader.GetFieldDefinition(field).DecodeSignature(this, null)["System.".Length..]);
            }
            // Resolve enum storage from metadata; guessing Int32 can hide a later Type argument.
            TypeReferenceHandle reference = reader.TypeReferences.FirstOrDefault(item => TypeReferenceName(item) == type);
            ExportedTypeHandle exported = reader.ExportedTypes.FirstOrDefault(item => ExportedName(item) == type);
            EntityHandle scope = !reference.IsNil ? reader.GetTypeReference(reference).ResolutionScope
                : !exported.IsNil ? reader.GetExportedType(exported).Implementation : default;
            while (scope.Kind == HandleKind.TypeReference) { scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope; }
            while (scope.Kind == HandleKind.ExportedType) { scope = reader.GetExportedType((ExportedTypeHandle)scope).Implementation; }
            if (scope.Kind != HandleKind.AssemblyReference) { throw new BadImageFormatException($"Metadata.Enum: unresolved {type}"); }
            string assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
            string? path = assembly is "System.Runtime" or "netstandard" or "mscorlib" ? typeof(object).Assembly.Location
                : ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator).FirstOrDefault(item => Path.GetFileNameWithoutExtension(item) == assembly);
            path ??= directory is null ? throw new BadImageFormatException($"Metadata.Enum: missing {assembly} for {type}") : Path.Combine(directory, assembly + ".dll");
            using FileStream stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            return new Names(pe.GetMetadataReader(), _ => { }, Path.GetDirectoryName(path)).GetUnderlyingEnumType(type);
        }
        private string TypeName(TypeDefinitionHandle handle)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            return type.GetDeclaringType().IsNil ? Qualify(reader.GetString(type.Namespace), reader.GetString(type.Name)) : TypeName(type.GetDeclaringType()) + "+" + reader.GetString(type.Name);
        }
        private string TypeReferenceName(TypeReferenceHandle handle)
        {
            TypeReference type = reader.GetTypeReference(handle);
            return type.ResolutionScope.Kind == HandleKind.TypeReference ? TypeReferenceName((TypeReferenceHandle)type.ResolutionScope) + "+" + reader.GetString(type.Name) : Qualify(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        private string ExportedName(ExportedTypeHandle handle)
        {
            ExportedType type = reader.GetExportedType(handle);
            return type.Implementation.Kind == HandleKind.ExportedType ? ExportedName((ExportedTypeHandle)type.Implementation) + "+" + reader.GetString(type.Name) : Qualify(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
    }
}
