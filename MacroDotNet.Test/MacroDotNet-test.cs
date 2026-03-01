using MacroDotNet.Test;
using System.Collections.Generic;

#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable IDE1006  // Naming Styles

return FUnit.Run(args, describe =>
{
    describe("MacroDotNet: replacement scenarios", it =>
    {
        it("Replaces field/type/visibility/static tokens", () =>
        {
            var privateProp = typeof(MacroSyntaxFixture).GetProperty("FieldValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Must.BeTrue(privateProp != null);
            Must.BeEqual(12, (int)privateProp!.GetValue(null)!);
            Must.BeEqual(12, MacroSyntaxFixture.PublicFieldValue);
        });

        it("Replaces $typeArgs and $0", () =>
        {
            var fixture = new MacroSyntaxFixture();
            Must.BeTrue(fixture.ItemsAsEnumerable is IEnumerable<string>);
            Must.BeEqual("<int, string>", fixture.TypeArgsMap);
            Must.BeEqual("<int?, object?>", fixture.TypeArgsDictNullable);
            Must.BeEqual("payload-ok", fixture.PayloadHolder);
            Must.BeEqual("Dictionary", fixture.BareTypeNullableGeneric);
            Must.BeEqual("global::MacroDotNet.Test.MacroSyntaxFixture", fixture.ContainerContainerSlot);
        });

        it("Replaces $initialValue including null/default/new()", () =>
        {
            var fixture = new MacroSyntaxFixture();
            Must.BeEqual<string?>(null, fixture.RawName);
            Must.BeEqual(7, MacroSyntaxFixture.FromConstConstValue);
            Must.BeEqual(0, fixture.DefaultInitializer);
            Must.BeEqual(0, fixture.InitialValueDefaultLiteral);
            Must.BeEqual(0, fixture.InitialValueListInit.Count);
            Must.BeTrue(!ReferenceEquals(fixture.InitialValueListInit, fixture.InitialValueListInit));
        });

        it("Replaces sugar tokens", () =>
        {
            var start = MacroSyntaxFixture.CurrentCounter;
            MacroSyntaxFixture.IncrementCounter();
            Must.BeTrue(MacroSyntaxFixture.CurrentCounter > start);

            Must.BeEqual(true, new MacroSyntaxFixture().RunTaskSlot().IsCompletedSuccessfully);
            Must.BeEqual(true, new MacroSyntaxFixture().RunValueTaskSlot().IsCompletedSuccessfully);
        });

        it("Supports constant attribute expressions", () =>
        {
            var fixture = new MacroSyntaxFixture();

            Must.BeEqual("ok", fixture.FromConcatConstExpr);
            Must.BeEqual("abc", fixture.ConstPayloadConstExpr);
            Must.BeEqual(nameof(MacroSyntaxFixture), fixture.NamePayloadNameofExpr);
            Must.BeEqual("const-payload", fixture.ConstRefPayloadConstRefExpr);
            Must.BeEqual("A" + nameof(MacroArgTemplates) + "Z", fixture.CombinedPayloadNameAndConcatExpr);
            Must.BeEqual("AB|CMacroArgTemplates|DEF", fixture.ComplexArgsComplexArgs);
        });

        it("Allows $-tokens inside $0 args", () =>
        {
            var fixture = new MacroSyntaxFixture();
            Must.BeEqual("_payloadVariableExpr", fixture.PayloadTokenPayloadVariableExpr);
        });

        it("Supports $0-$9 indexed args replacement", () =>
        {
            var fixture = new MacroSyntaxFixture();
            Must.BeEqual("ABJ", fixture.ArgsArgs);
        });

        it("Supports reusable notify template (static and instance)", () =>
        {
            MacroSyntaxFixture.SetFooWithoutNotify(10);

            var fixture = new MacroSyntaxFixture();
            fixture.SetBar(123);

            var fooField = typeof(MacroSyntaxFixture).GetField("_foo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var barField = typeof(MacroSyntaxFixture).GetField("_bar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Must.BeEqual(10, (int)fooField!.GetValue(null)!);
            Must.BeEqual(123, (int)barField!.GetValue(fixture)!);
            Must.BeEqual("_bar", MacroSyntaxFixture.LatestChangedField);
        });

        it("Supports injected validation and throws", () =>
        {
            var fixture = new MacroSyntaxFixture();
            Must.Throw<System.ArgumentOutOfRangeException>(
                "Specified argument was out of the range of valid values. (Parameter '_valueMustBePositive')",
                () => fixture.SetValueMustBePositive(0));
            Must.Throw<System.ArgumentOutOfRangeException>(
                "Specified argument was out of the range of valid values. (Parameter '_valueMustBePositive')",
                () => fixture.SetValueMustBePositiveWithoutNotify(-1));
            fixture.SetValueMustBePositive(1);
        });
    });

    describe("MacroDotNet: nullable marker stripping", it =>
    {
        it("Strips trailing ? from generated type tokens", () =>
        {
            var fieldType = typeof(MacroSyntaxFixture).GetField(nameof(MacroSyntaxFixture._nullableInt))!.FieldType;
            var propertyType = typeof(MacroSyntaxFixture).GetProperty(nameof(MacroSyntaxFixture.NonNullableNullableInt))!.PropertyType;

            Must.BeEqual("Nullable`1", fieldType.Name);
            Must.BeEqual("Int32", propertyType.Name);
        });
    });

    describe("MacroDotNet: container type token", it =>
    {
        it("Resolves generic-aware full name in $containerType.$fieldName", () =>
        {
            var fixture = new OuterClass.NestedClass.MacroGenericFixture<int>();
            Must.BeEqual("global::MacroDotNet.Test.OuterClass.NestedClass.MacroGenericFixture<T>._value", fixture.FullName);
        });
    });

    describe("MacroDotNet: type constraints token", it =>
    {
        it("Resolves field-type constraints in $typeConstraints", () =>
        {
            var fixture = new MacroTypeConstraintFixture();
            Must.BeEqual("where TItem : unmanaged", fixture.TypeConstraintsText);
            Must.BeEqual("where TItem : class?", fixture.TypeConstraintsNullableText);
            Must.BeEqual("where TItem : class", fixture.TypeConstraintsClassText);
            Must.BeEqual("where TItem : global::MacroDotNet.Test.MyClass", fixture.TypeConstraintsBaseClassText);
            Must.BeEqual("where TItem : global::MacroDotNet.Test.MyClass?", fixture.TypeConstraintsNullableBaseClassText);
            Must.BeEqual("where TItem : class?, global::System.IDisposable, new() where TValue : notnull", fixture.TypeConstraintsComplexText);
            Must.BeEqual("where T : TOther", fixture.TypeConstraintsTypeParameterText);
            Must.BeEqual(string.Empty, fixture.TypeConstraintsNonGenericText);
        });
    });

    describe("MacroDotNet: token fixture", it =>
    {
        it("Replaces tokens one by one as expected", () =>
        {
            var fixture = new MacroTokenFixture();

            Must.BeEqual("_fieldName", fixture.TokenFieldName);
            Must.BeEqual("Display_name", fixture.TokenDisplayName);
            Must.BeEqual("Bar", fixture.TokenDisplayNameFooBar);
            Must.BeEqual("Blah_blah", fixture.TokenDisplayNameBlah);
            Must.BeEqual("global::System.Collections.Generic.List<int?>", fixture.TokenTypeName);
            Must.BeEqual("List<int?>", fixture.TokenTypeShortName);
            Must.BeEqual("List", fixture.TokenTypeBareName);
            Must.BeEqual("static", MacroTokenFixture.TokenStatic);
            Must.BeEqual("private", fixture.TokenVisibility);
            Must.BeEqual("5", fixture.TokenInitialValue);
            Must.BeEqual("<int?>", fixture.TokenTypeArgs);
            Must.BeEqual("global::MacroDotNet.Test.MacroTokenFixture", fixture.TokenContainerType);
            Must.BeEqual("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]", fixture.TokenInline);
            Must.BeEqual("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]", fixture.TokenNoInline);

            Must.BeEqual(string.Empty, fixture.TokenArg1Missing);
            Must.BeEqual("A", fixture.TokenArg0);
            Must.BeEqual("B", fixture.TokenArg1);
            Must.BeEqual("C", fixture.TokenArg2);
            Must.BeEqual("D", fixture.TokenArg3);
            Must.BeEqual("E", fixture.TokenArg4);
            Must.BeEqual("F", fixture.TokenArg5);
            Must.BeEqual("G", fixture.TokenArg6);
            Must.BeEqual("H", fixture.TokenArg7);
            Must.BeEqual("I", fixture.TokenArg8);
            Must.BeEqual("J", fixture.TokenArg9);
            Must.BeEqual("$$ABCDEFGHIJ$$$", fixture.TokenArgDuplicates);
            Must.BeEqual("$fieldName", fixture.TokenEscapedFieldName);
        });
    });
});
