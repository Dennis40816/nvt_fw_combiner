using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace NvtFwCombiner.Architecture.Tests.Metadata;

internal static class MetadataFixtureBuilder
{
    internal const string Presentation = "NvtFwCombiner.Presentation.Avalonia";
    internal const string ViewModel = Presentation + ".ViewModels.ForbiddenViewModel";
    internal const string Control = "Avalonia.Controls.ForbiddenControl";

    internal static ImmutableArray<byte> Create(string scenario = "Positive", string owner = ViewModel, string assembly = "Fixture")
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var metadata = new MetadataBuilder();
        _ = metadata.AddModule(0, metadata.GetOrAddString(assembly + ".dll"), metadata.GetOrAddGuid(new Guid("a1111111-1111-1111-1111-111111111111")), default, default);
        _ = metadata.AddAssembly(metadata.GetOrAddString(assembly), new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        AssemblyReferenceHandle core = metadata.AddAssemblyReference(metadata.GetOrAddString("System.Runtime"), new Version(10, 0), default, default, 0, default);
        TypeReferenceHandle Type(string name)
        {
            int split = name.LastIndexOf('.');
            return metadata.AddTypeReference(core, metadata.GetOrAddString(name[..split]), metadata.GetOrAddString(name[(split + 1)..]));
        }
        TypeReferenceHandle objectType = Type("System.Object");
        TypeReferenceHandle target = Type(scenario is "Control" or "Service" or "View" ? ViewModel : scenario.StartsWith("Json", StringComparison.Ordinal) ? (scenario == "JsonParse" ? "System.Text.Json.JsonDocument" : "System.Text.Json.JsonSerializer") : Control);
        TypeReferenceHandle list = Type("System.Collections.Generic.List`1");
        BlobHandle Signature(Action<BlobBuilder> write)
        {
            var blob = new BlobBuilder();
            write(blob);
            return metadata.GetOrAddBlob(blob);
        }
        void Class(BlobBuilder blob, EntityHandle type)
        {
            blob.WriteByte(0x12);
            blob.WriteCompressedInteger(type.Kind == HandleKind.TypeSpecification ? (MetadataTokens.GetRowNumber(type) << 2) | 2 : (MetadataTokens.GetRowNumber(type) << 2) | 1);
        }
        BlobHandle fieldSignature = Signature(blob => { blob.WriteByte(6); Class(blob, target); });
        BlobHandle methodSignature = Signature(blob =>
        {
            blob.WriteByte(scenario == "MethodConstraint" ? (byte)0x10 : (byte)0);
            if (scenario == "MethodConstraint") { blob.WriteByte(1); }
            blob.WriteByte(scenario is "Parameter" or "NestedGeneric" or "Control" or "Service" or "View" ? (byte)1 : (byte)0);
            if (scenario == "Return") { Class(blob, target); } else { blob.WriteByte(1); }
            if (scenario is "NestedGeneric" or "Service") { blob.WriteByte(0x15); Class(blob, list); blob.WriteByte(1); Class(blob, target); }
            else if (scenario is "Parameter" or "Control" or "Service" or "View") { Class(blob, target); }
        });
        _ = metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        int dot = owner.LastIndexOf('.');
        TypeDefinitionHandle definition = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString(owner[..dot]), metadata.GetOrAddString(owner[(dot + 1)..]),
            scenario == "Base" ? target : scenario == "View" ? Type("Avalonia.Controls.UserControl") : objectType, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        if (scenario == "Interface") { _ = metadata.AddInterfaceImplementation(definition, target); }
        if (scenario == "Field") { _ = metadata.AddFieldDefinition(FieldAttributes.Public, metadata.GetOrAddString("Field"), fieldSignature); }
        if (scenario == "Property")
        {
            PropertyDefinitionHandle property = metadata.AddProperty(PropertyAttributes.None, metadata.GetOrAddString("Property"), Signature(blob => { blob.WriteByte(8); blob.WriteByte(0); Class(blob, target); }));
            metadata.AddPropertyMap(definition, property);
        }
        if (scenario == "Event")
        {
            EventDefinitionHandle eventHandle = metadata.AddEvent(EventAttributes.None, metadata.GetOrAddString("Event"), target);
            metadata.AddEventMap(definition, eventHandle);
        }
        var il = new BlobBuilder();
        var flow = new ControlFlowBuilder();
        var code = new InstructionEncoder(il, flow);
        StandaloneSignatureHandle locals = default;
        if (scenario == "Local") { locals = metadata.AddStandaloneSignature(Signature(blob => { blob.WriteByte(7); blob.WriteByte(1); Class(blob, target); })); }
        if (scenario == "Catch")
        {
            LabelHandle start = code.DefineLabel(), end = code.DefineLabel(), handler = code.DefineLabel(), handlerEnd = code.DefineLabel();
            code.MarkLabel(start); code.OpCode(ILOpCode.Nop); code.Branch(ILOpCode.Leave_s, handlerEnd);
            code.MarkLabel(end); code.MarkLabel(handler); code.OpCode(ILOpCode.Pop); code.Branch(ILOpCode.Leave_s, handlerEnd); code.MarkLabel(handlerEnd);
            flow.AddCatchRegion(start, end, handler, handlerEnd, target);
        }
        if (scenario is "TypeToken" or "TypeSpecification") { code.OpCode(ILOpCode.Ldtoken); code.Token(scenario == "TypeSpecification" ? metadata.AddTypeSpecification(Signature(value => { value.WriteByte(0x1d); Class(value, target); })) : target); code.OpCode(ILOpCode.Pop); }
        if (scenario == "FieldToken")
        {
            MemberReferenceHandle field = metadata.AddMemberReference(objectType, metadata.GetOrAddString("ForbiddenField"), fieldSignature);
            code.OpCode(ILOpCode.Ldsfld); code.Token(field); code.OpCode(ILOpCode.Pop);
        }
        if (scenario is "MemberReference" or "MethodSpecification" or "JsonParse" or "JsonDeserialize" or "JsonDeserializeAsync")
        {
            MemberReferenceHandle method = metadata.AddMemberReference(target, metadata.GetOrAddString(scenario == "JsonParse" ? "Parse" : scenario.StartsWith("Json", StringComparison.Ordinal) ? scenario[4..] : "ForbiddenCall"),
                Signature(blob => { blob.WriteByte(scenario == "MethodSpecification" ? (byte)0x10 : (byte)0); if (scenario == "MethodSpecification") { blob.WriteByte(1); } blob.WriteByte(0); blob.WriteByte(1); }));
            EntityHandle token = method;
            if (scenario == "MethodSpecification") { token = metadata.AddMethodSpecification(method, Signature(blob => { blob.WriteByte(0x0a); blob.WriteByte(1); Class(blob, target); })); }
            code.OpCode(ILOpCode.Call); code.Token(token);
        }
        if (scenario == "Calli")
        {
            StandaloneSignatureHandle signature = metadata.AddStandaloneSignature(Signature(blob => { blob.WriteByte(0); blob.WriteByte(0); Class(blob, target); }));
            code.OpCode(ILOpCode.Ldc_i4_0); code.OpCode(ILOpCode.Conv_i); code.OpCode(ILOpCode.Calli); code.Token(signature); code.OpCode(ILOpCode.Pop);
        }
        code.OpCode(ILOpCode.Ret);
        var bodies = new BlobBuilder();
        int body = new MethodBodyStreamEncoder(bodies).AddMethodBody(code, localVariablesSignature: locals);
        MethodDefinitionHandle methodHandle = metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL, metadata.GetOrAddString("Probe"), methodSignature, body, MetadataTokens.ParameterHandle(1));
        if (scenario is "TypeConstraint" or "MethodConstraint")
        {
            GenericParameterHandle generic = metadata.AddGenericParameter(scenario == "TypeConstraint" ? definition : methodHandle, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
            _ = metadata.AddGenericParameterConstraint(generic, target);
        }
        if (scenario is "Attribute" or "NamedAttribute" or "AttributeArray" or "AttributeEnum")
        {
            TypeDefinitionHandle enumType = default;
            if (scenario == "AttributeEnum")
            {
                enumType = metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Sealed, metadata.GetOrAddString("Fixtures"), metadata.GetOrAddString("ByteEnum"), Type("System.Enum"), MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(2));
                _ = metadata.AddFieldDefinition(FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, metadata.GetOrAddString("value__"), Signature(blob => { blob.WriteByte(6); blob.WriteByte(5); }));
            }
            TypeReferenceHandle attributeType = Type("Fixtures.TypeArgumentAttribute");
            TypeReferenceHandle systemType = Type("System.Type");
            MemberReferenceHandle ctor = metadata.AddMemberReference(attributeType, metadata.GetOrAddString(".ctor"), Signature(blob =>
            {
                blob.WriteByte(0x20); blob.WriteByte(scenario == "NamedAttribute" ? (byte)0 : scenario == "AttributeEnum" ? (byte)2 : (byte)1); blob.WriteByte(1);
                if (scenario == "AttributeEnum") { blob.WriteByte(0x11); blob.WriteCompressedInteger(MetadataTokens.GetRowNumber(enumType) << 2); }
                if (scenario != "NamedAttribute") { if (scenario == "AttributeArray") { blob.WriteByte(0x1d); } Class(blob, systemType); }
            }));
            _ = metadata.AddCustomAttribute(definition, ctor, Signature(blob =>
            {
                blob.WriteUInt16(1);
                if (scenario == "AttributeEnum") { blob.WriteByte(42); }
                if (scenario == "AttributeArray") { blob.WriteInt32(1); }
                if (scenario != "NamedAttribute") { blob.WriteSerializedString(Control + ", Fixtures"); }
                blob.WriteUInt16(scenario == "NamedAttribute" ? (ushort)1 : (ushort)0);
                if (scenario == "NamedAttribute") { blob.WriteByte(0x54); blob.WriteByte(0x50); blob.WriteSerializedString("Type"); blob.WriteSerializedString(Control + ", Fixtures"); }
            }));
        }
        var image = new BlobBuilder();
        new ManagedPEBuilder(new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll), new MetadataRootBuilder(metadata), bodies, deterministicIdProvider: _ => new BlobContentId(new Guid("b1111111-1111-1111-1111-111111111111"), 0)).Serialize(image);
        return [.. image.ToArray()];
    }
}
