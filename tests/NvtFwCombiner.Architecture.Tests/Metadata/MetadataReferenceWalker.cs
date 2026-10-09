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
    private readonly Dictionary<string, MethodDefinitionHandle> _stateMachines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<MethodDefinitionHandle>> _referrers = new(StringComparer.Ordinal);
    private readonly Dictionary<MethodDefinitionHandle, string> _logical = [];
    private readonly HashSet<MethodDefinitionHandle> _resolving = [];
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
        string arity = signature.GenericParameterCount == 0 ? "" : "`" + signature.GenericParameterCount;
        return $"{_reader.GetString(method.Name)}{arity}({string.Join(",", signature.ParameterTypes)})";
    }
    private ImmutableArray<MetadataReference> Walk()
    {
        // Use the actual Async/IteratorStateMachineAttribute link, not a generated ordinal.
        // Closures are tied to their enclosing member by the IL that creates or calls them.
        foreach (TypeDefinitionHandle type in _reader.TypeDefinitions)
        {
            foreach (MethodDefinitionHandle handle in _reader.GetTypeDefinition(type).GetMethods())
            {
                int rva = _reader.GetMethodDefinition(handle).RelativeVirtualAddress;
                if (rva != 0) { WalkIl(_pe.GetMethodBody(rva).GetILReader(), token => Refer(handle, token)); }
                foreach (CustomAttributeHandle attribute in _reader.GetMethodDefinition(handle).GetCustomAttributes())
                {
                    CustomAttribute data = _reader.GetCustomAttribute(attribute);
                    string name = ConstructorType(data.Constructor);
                    if (name is "System.Runtime.CompilerServices.AsyncStateMachineAttribute" or "System.Runtime.CompilerServices.IteratorStateMachineAttribute" or "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute")
                    {
                        string generated = (string)data.DecodeValue(_silent).FixedArguments[0].Value!;
                        _stateMachines.Add(generated, handle);
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
                _member = _stateMachines.TryGetValue(name, out MethodDefinitionHandle owner) ? LogicalMethod(owner) : LogicalMethod(method);
                MethodDefinition item = _reader.GetMethodDefinition(method);
                _ = item.DecodeSignature(_names, null);
                Attributes(item.GetCustomAttributes());
                Constraints(item.GetGenericParameters());
                foreach (ParameterHandle parameter in item.GetParameters()) { Attributes(_reader.GetParameter(parameter).GetCustomAttributes()); }
                if (item.RelativeVirtualAddress == 0) { continue; }
                MethodBodyBlock body = _pe.GetMethodBody(item.RelativeVirtualAddress);
                if (!body.LocalSignature.IsNil) { _ = _reader.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(_names, null); }
                foreach (ExceptionRegion region in body.ExceptionRegions) { Token(region.CatchType); }
                WalkIl(body.GetILReader(), Token);
            }
        }
        return [.. _references.OrderBy(item => item.Owner, StringComparer.Ordinal).ThenBy(item => item.Member, StringComparer.Ordinal)
            .ThenBy(item => item.Target, StringComparer.Ordinal).ThenBy(item => item.TargetMember, StringComparer.Ordinal)];
    }
    private (string Owner, string Member) LogicalOwner(TypeDefinitionHandle handle)
    {
        string name = _silent.Type(handle);
        if (_stateMachines.TryGetValue(name, out MethodDefinitionHandle owner))
        {
            return (LogicalOwner(_reader.GetMethodDefinition(owner).GetDeclaringType()).Owner, LogicalMethod(owner));
        }
        TypeDefinition type = _reader.GetTypeDefinition(handle);
        string simple = _reader.GetString(type.Name);
        return !type.GetDeclaringType().IsNil && simple.StartsWith('<') ? LogicalOwner(type.GetDeclaringType()) : (name, "");
    }
    private string LogicalMethod(MethodDefinitionHandle handle)
    {
        if (_logical.TryGetValue(handle, out string? known)) { return known; }
        MethodDefinition method = _reader.GetMethodDefinition(handle);
        string name = _reader.GetString(method.Name);
        TypeDefinition declaring = _reader.GetTypeDefinition(method.GetDeclaringType());
        string result;
        if (IsClosureName(name))
        {
            _ = _resolving.Add(handle);
            result = Closure(handle);
            _ = _resolving.Remove(handle);
        }
        else
        {
            result = !declaring.GetDeclaringType().IsNil && _reader.GetString(declaring.Name).StartsWith('<') ? name + " [generated]" : MethodName(handle);
        }
        _logical[handle] = result;
        return result;
    }
    // Lambdas and local functions: <Enclosing>b__N_M and <Enclosing>g__Local|N_M.
    private static bool IsClosureName(string name)
    {
        int end = name.IndexOf('>', StringComparison.Ordinal);
        return name.StartsWith('<') && end > 1 && (name.AsSpan(end + 1).StartsWith("b__", StringComparison.Ordinal) || name.AsSpan(end + 1).StartsWith("g__", StringComparison.Ordinal));
    }
    private bool IsGenerated(MethodDefinitionHandle handle)
    {
        MethodDefinition method = _reader.GetMethodDefinition(handle);
        return _reader.GetString(method.Name).StartsWith('<') || _reader.GetString(_reader.GetTypeDefinition(method.GetDeclaringType()).Name).StartsWith('<');
    }
    private string Closure(MethodDefinitionHandle handle)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        Collect(handle, found, []);
        // Several creators (for example a field initializer shared by constructors) all stay in the identity.
        return found.Count == 0
            ? throw new BadImageFormatException($"Metadata.Closure: no enclosing member for {_silent.Type(_reader.GetMethodDefinition(handle).GetDeclaringType())}::{_reader.GetString(_reader.GetMethodDefinition(handle).Name)}")
            : string.Join("|", found) + " [closure]";
    }
    private void Collect(MethodDefinitionHandle generated, SortedSet<string> found, HashSet<MethodDefinitionHandle> seen)
    {
        if (!seen.Add(generated)) { return; }
        MethodDefinition method = _reader.GetMethodDefinition(generated);
        TypeDefinition declaring = _reader.GetTypeDefinition(method.GetDeclaringType());
        string type = Generic(_silent.Type(method.GetDeclaringType()));
        string[] keys = _reader.GetString(declaring.Name).Contains("DisplayClass", StringComparison.Ordinal)
            ? [type + "::" + _reader.GetString(method.Name), type]
            : [type + "::" + _reader.GetString(method.Name)];
        // A display class belongs to one scope, so its creators are creators of the closure too.
        foreach (string key in keys)
        {
            if (!_referrers.TryGetValue(key, out HashSet<MethodDefinitionHandle>? from)) { continue; }
            foreach (MethodDefinitionHandle referrer in from.Where(item => item != generated))
            {
                if (_stateMachines.TryGetValue(_silent.Type(_reader.GetMethodDefinition(referrer).GetDeclaringType()), out MethodDefinitionHandle owner))
                {
                    if (!_resolving.Contains(owner)) { _ = found.Add(LogicalMethod(owner)); }
                }
                else if (IsGenerated(referrer)) { Collect(referrer, found, seen); }
                else { _ = found.Add(MethodName(referrer)); }
            }
        }
    }
    private static string Generic(string type)
    {
        // A constructed generic type refers to its definition by the name without the argument list.
        if (!type.EndsWith('>')) { return type; }
        int depth = 0;
        for (int index = type.Length - 1; index > 0; index--)
        {
            if (type[index] == '>') { depth++; }
            else if (type[index] == '<' && --depth == 0) { return type[index - 1] == '+' ? type : type[..index]; }
        }
        return type;
    }
    private void Refer(MethodDefinitionHandle from, EntityHandle token)
    {
        string[] keys;
        if (token.Kind == HandleKind.MethodDefinition)
        {
            MethodDefinition definition = _reader.GetMethodDefinition((MethodDefinitionHandle)token);
            keys = Keys(_silent.Type(definition.GetDeclaringType()), _reader.GetString(definition.Name));
        }
        else if (token.Kind == HandleKind.MemberReference)
        {
            MemberReference member = _reader.GetMemberReference((MemberReferenceHandle)token);
            keys = Keys(_silent.Type(member.Parent), _reader.GetString(member.Name));
        }
        else if (token.Kind == HandleKind.MethodSpecification)
        {
            Refer(from, _reader.GetMethodSpecification((MethodSpecificationHandle)token).Method);
            return;
        }
        else if (token.Kind == HandleKind.FieldDefinition) { keys = Keys(_silent.Type(_reader.GetFieldDefinition((FieldDefinitionHandle)token).GetDeclaringType()), ""); }
        else if (token.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification) { keys = Keys(_silent.Type(token), ""); }
        else { return; }
        foreach (string key in keys)
        {
            if (!_referrers.TryGetValue(key, out HashSet<MethodDefinitionHandle>? set)) { _referrers[key] = set = []; }
            _ = set.Add(from);
        }
    }
    private static string[] Keys(string type, string member)
    {
        string name = Generic(type);
        return member.Length == 0 ? [name] : [name, name + "::" + member];
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
            case HandleKind.ModuleDefinition:
                break;
            case HandleKind.Parameter:
                break;
            case HandleKind.InterfaceImplementation:
                break;
            case HandleKind.Constant:
                break;
            case HandleKind.CustomAttribute:
                break;
            case HandleKind.DeclarativeSecurityAttribute:
                break;
            case HandleKind.EventDefinition:
                break;
            case HandleKind.PropertyDefinition:
                break;
            case HandleKind.MethodImplementation:
                break;
            case HandleKind.ModuleReference:
                break;
            case HandleKind.AssemblyDefinition:
                break;
            case HandleKind.AssemblyReference:
                break;
            case HandleKind.AssemblyFile:
                break;
            case HandleKind.ExportedType:
                break;
            case HandleKind.ManifestResource:
                break;
            case HandleKind.GenericParameter:
                break;
            case HandleKind.GenericParameterConstraint:
                break;
            case HandleKind.Document:
                break;
            case HandleKind.MethodDebugInformation:
                break;
            case HandleKind.LocalScope:
                break;
            case HandleKind.LocalVariable:
                break;
            case HandleKind.LocalConstant:
                break;
            case HandleKind.ImportScope:
                break;
            case HandleKind.CustomDebugInformation:
                break;
            case HandleKind.UserString:
                break;
            case HandleKind.Blob:
                break;
            case HandleKind.Guid:
                break;
            case HandleKind.String:
                break;
            case HandleKind.NamespaceDefinition:
                break;
            default: throw new BadImageFormatException($"Metadata.Token: unsupported {handle.Kind}");
        }
    }
    private static void WalkIl(BlobReader il, Action<EntityHandle> token)
    {
        while (il.RemainingBytes > 0)
        {
            int op = il.ReadByte();
            if (op == 0xfe) { op = 0xfe00 | il.ReadByte(); }
            // ECMA-335 III operand encodings. Prefixes, calli and ldtoken are included.
            if (op is 0x27 or 0x28 or 0x29 or 0x6f or 0x70 or 0x71 or 0x73 or 0x74 or 0x75 or 0x79
                or (>= 0x7b and <= 0x81) or 0x8c or 0x8d or 0x8f or 0xa3 or 0xa4 or 0xa5 or 0xc2 or 0xc6 or 0xd0
                or 0xfe06 or 0xfe07 or 0xfe15 or 0xfe16 or 0xfe1c) { token(MetadataTokens.EntityHandle(il.ReadInt32())); }
            else if (op == 0x45) { int count = il.ReadInt32(); il.Offset = checked(il.Offset + checked(count * 4)); }
            else
            {
                int size = op switch
                {
                    0x20 or 0x22 or (>= 0x38 and <= 0x44) or 0x72 or 0xdd => 4,
                    0x21 or 0x23 => 8,
                    (>= 0x0e and <= 0x13) or 0x1f or (>= 0x2b and <= 0x37) or 0xde or 0xfe12 or 0xfe19 => 1,
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
                HandleKind.ModuleDefinition => throw new NotImplementedException(),
                HandleKind.FieldDefinition => throw new NotImplementedException(),
                HandleKind.Parameter => throw new NotImplementedException(),
                HandleKind.InterfaceImplementation => throw new NotImplementedException(),
                HandleKind.MemberReference => throw new NotImplementedException(),
                HandleKind.Constant => throw new NotImplementedException(),
                HandleKind.CustomAttribute => throw new NotImplementedException(),
                HandleKind.DeclarativeSecurityAttribute => throw new NotImplementedException(),
                HandleKind.StandaloneSignature => throw new NotImplementedException(),
                HandleKind.EventDefinition => throw new NotImplementedException(),
                HandleKind.PropertyDefinition => throw new NotImplementedException(),
                HandleKind.MethodImplementation => throw new NotImplementedException(),
                HandleKind.AssemblyDefinition => throw new NotImplementedException(),
                HandleKind.AssemblyReference => throw new NotImplementedException(),
                HandleKind.AssemblyFile => throw new NotImplementedException(),
                HandleKind.ExportedType => throw new NotImplementedException(),
                HandleKind.ManifestResource => throw new NotImplementedException(),
                HandleKind.GenericParameter => throw new NotImplementedException(),
                HandleKind.MethodSpecification => throw new NotImplementedException(),
                HandleKind.GenericParameterConstraint => throw new NotImplementedException(),
                HandleKind.Document => throw new NotImplementedException(),
                HandleKind.MethodDebugInformation => throw new NotImplementedException(),
                HandleKind.LocalScope => throw new NotImplementedException(),
                HandleKind.LocalVariable => throw new NotImplementedException(),
                HandleKind.LocalConstant => throw new NotImplementedException(),
                HandleKind.ImportScope => throw new NotImplementedException(),
                HandleKind.CustomDebugInformation => throw new NotImplementedException(),
                HandleKind.UserString => throw new NotImplementedException(),
                HandleKind.Blob => throw new NotImplementedException(),
                HandleKind.Guid => throw new NotImplementedException(),
                HandleKind.String => throw new NotImplementedException(),
                HandleKind.NamespaceDefinition => throw new NotImplementedException(),
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
        private static string Qualify(string ns, string name)
        {
            return ns.Length == 0 ? name : ns + "." + name;
        }

        public string GetTypeFromSpecification(MetadataReader metadata, object? context, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return metadata.GetTypeSpecification(handle).DecodeSignature(this, context);
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> arguments)
        {
            return genericType + "<" + string.Join(",", arguments) + ">";
        }

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType + "[" + new string(',', shape.Rank - 1) + "]";
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType + "&";
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetGenericMethodParameter(object? context, int index)
        {
            return "!!" + index;
        }

        public string GetGenericTypeParameter(object? context, int index)
        {
            return "!" + index;
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "fn(" + string.Join(",", signature.ParameterTypes) + ")->" + signature.ReturnType;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return "System." + typeCode;
        }

        public string GetSystemType()
        {
            return "System.Type";
        }

        public bool IsSystemType(string type)
        {
            return type == GetSystemType();
        }

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
