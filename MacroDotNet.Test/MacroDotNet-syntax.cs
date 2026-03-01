// Test syntax fixtures for MacroDotNet generator.

using System.Collections.Generic;

#pragma warning disable CA1050   // Declare types in namespaces
#pragma warning disable IDE1006  // Naming Styles
#pragma warning disable IDE0044  // Add readonly modifier
#pragma warning disable IDE0052  // Remove unread private members
#pragma warning disable IDE0051  // Remove unused private members
#pragma warning disable IDE0032  // Use auto property
#pragma warning disable CA1051   // Do not declare visible instance fields

namespace MacroDotNet.Test;

public partial class MacroSyntaxFixture
{
    // Reusable notify interface (zero-overhead)
    private const string NotifyTemplate = """
        public $static void Set$displayName($typeName value)
        {
            $0
            if ($fieldName == value) return;
            $fieldName = value;
            NotifyChanged("$fieldName");
        }
        public $static void Set$displayNameWithoutNotify($typeName value)
        {
            $0
            if ($fieldName == value) return;
            $fieldName = value;
        }
        """;
    [Macro(NotifyTemplate)] private static int _foo;
    [Macro(NotifyTemplate)] private int _bar;
    [Macro(NotifyTemplate, "if (value <= 0) throw new ArgumentOutOfRangeException(\"$fieldName\");")]
    private int _valueMustBePositive;

    private static string _latestChangedField = string.Empty;
    private static void NotifyChanged(string fieldName)
    {
        System.Threading.Interlocked.Exchange(ref _latestChangedField, fieldName);
    }
    public static string LatestChangedField => _latestChangedField;

    [Macro("$visibility $static $typeName Field$displayName => $fieldName;")]
    [Macro("public static $typeName PublicField$displayName => $fieldName;")]
    private static int _value = 12;

    [Macro("public IEnumerable$typeArgs $displayNameAsEnumerable => $fieldName;")]
    private List<string> _items = new();

    [Macro("public string TypeArgs$displayName => \"$typeArgs\";")]
    private Dictionary<int, string> _map = new();

    [Macro("public string TypeArgs$displayName => \"$typeArgs\";")]
    private Dictionary<int?, object?>? _dictNullable;

    [Macro("public string Payload$displayName => \"$0\";", "payload-ok")]
    private int payload_holder;

    [Macro("public $typeShortName Raw$displayName => $initialValue;")]
    private string? _name = null;

    [Macro("public int Default$displayName => $initialValue;")]
    private int no_initializer;

    [Macro("public int InitialValue$displayName => $initialValue;")]
    private int defaultLiteral = default;

    [Macro("public static $typeName FromConst$displayName => $fieldName;")]
    private const int _constValue = 7;

    [Macro("public $typeName InitialValue$displayName => $initialValue;")]
    private List<int> listInit = new();

    [Macro("public string BareType$displayName => \"$typeBareName\";")]
    private Dictionary<int, string>? nullableGeneric;

    [Macro("public string Container$displayName => \"$containerType\";")]
    private int _containerSlot;

    [Macro("public $typeShortName NonNullable$displayName => $fieldName ?? -1;")]
    public int? _nullableInt = 7;

    [Macro("public static int Current$displayName => $fieldName;")]
    [Macro("$inline public static void Increment$displayName() { Interlocked.Increment(ref _counter); }")]
    private static int _counter;

    [Macro("$noinline public Task Run$displayName() => Task.CompletedTask;")]
    private int _taskSlot;

    [Macro("public ValueTask Run$displayName() => ValueTask.CompletedTask;")]
    private int _valueTaskSlot;

    [Macro("public string FromConcat$displayName => \"ok\";")]
    [Macro("public string ConstPayload$displayName" + " => " + "\"$0\";", "a" + "b" + "c")]
    private int _constExpr;

    [Macro("public string NamePayload$displayName => \"$0\";", nameof(MacroSyntaxFixture))]
    private int _nameofExpr;

    [Macro(MacroArgTemplates.SourceTemplate, MacroArgTemplates.PayloadConst)]
    private int _constRefExpr;

    [Macro("public string CombinedPayload$displayName => \"$0\";", "A" + nameof(MacroArgTemplates) + "Z")]
    private int _nameAndConcatExpr;

    [Macro("public string PayloadToken$displayName => \"$0\";", "$fieldName")]
    private int _payloadVariableExpr;

    [Macro(
        "public string ComplexArgs$displayName => \"$0|$1|$2\";",
        "A" + "B",
        "C" + nameof(MacroArgTemplates),
        "D" + "E" + "F")]
    private int _complexArgs;

    [Macro("public string Args$displayName => \"$0$1$9\";",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J")]
    private int _args;
}

public static class MacroArgTemplates
{
    public const string SourceTemplate = "public string ConstRefPayload$displayName => \"$0\";";
    public const string PayloadConst = "const-payload";
}

public partial class OuterClass
{
    public sealed partial record NestedClass
    {
        public partial class MacroGenericFixture<T>
        {
            [Macro("public string FullName => \"$containerType.$fieldName\";")]
            private int _value;
        }
    }
}

public partial class MacroTokenFixture
{
    [Macro("public string TokenFieldName => \"$fieldName\";")]
    private int _fieldName;

    [Macro("public string TokenDisplayName => \"$displayName\";")]
    private int _display_name;

    [Macro("public string TokenDisplayNameFooBar => \"$displayName\";")]
    private int foo_bar;

    [Macro("public string TokenDisplayNameBlah => \"$displayName\";")]
    private int Blah_blah_blah;

    [Macro("public string TokenTypeName => \"$typeName\";")]
    private List<int?>? _typeName = new();

    [Macro("public string TokenTypeShortName => \"$typeShortName\";")]
    private List<int?>? _typeShortName = new();

    [Macro("public string TokenTypeBareName => \"$typeBareName\";")]
    private List<int?>? _typeBareName = new();

    [Macro("public static string TokenStatic => \"$static\";")]
    private static int _staticSlot;

    [Macro("public string TokenVisibility => \"$visibility\";")]
    private int _visibilitySlot;

    [Macro("public string TokenInitialValue => \"$initialValue\";")]
    private int _initialValue = 5;

    [Macro("public string TokenTypeArgs => \"$typeArgs\";")]
    private List<int?>? _typeArgs = new();

    [Macro("public string TokenContainerType => \"$containerType\";")]
    private int _containerType;

    [Macro("public string TokenInline => \"$inline\";")]
    private int _inline;

    [Macro("public string TokenNoInline => \"$noinline\";")]
    private int _noinline;


    [Macro("public string TokenArg1Missing => \"$1\";", "A")]
    private int _arg1Missing;


    [Macro("public string TokenArg0 => \"$0\";", "A")]
    private int _arg0;

    [Macro("public string TokenArg1 => \"$1\";", "A", "B")]
    private int _arg1;

    [Macro("public string TokenArg2 => \"$2\";", "A", "B", "C")]
    private int _arg2;

    [Macro("public string TokenArg3 => \"$3\";", "A", "B", "C", "D")]
    private int _arg3;

    [Macro("public string TokenArg4 => \"$4\";", "A", "B", "C", "D", "E")]
    private int _arg4;

    [Macro("public string TokenArg5 => \"$5\";", "A", "B", "C", "D", "E", "F")]
    private int _arg5;

    [Macro("public string TokenArg6 => \"$6\";", "A", "B", "C", "D", "E", "F", "G")]
    private int _arg6;

    [Macro("public string TokenArg7 => \"$7\";", "A", "B", "C", "D", "E", "F", "G", "H")]
    private int _arg7;

    [Macro("public string TokenArg8 => \"$8\";", "A", "B", "C", "D", "E", "F", "G", "H", "I")]
    private int _arg8;

    [Macro("public string TokenArg9 => \"$9\";", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J")]
    private int _arg9;

    [Macro("public string TokenArgDuplicates => \"$$$0$1$2$3$4$5$6$7$8$9$$$\";",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J")]
    private int _argDuplicates;

    [Macro(@"public string TokenEscapedFieldName => ""\u0024fieldName"";")]
    private int _escapedFieldName;
}

public partial class MacroConstrainedType<TItem>
    where TItem : unmanaged
{
}

public partial class MacroNullableConstrainedType<TItem>
    where TItem : class?
{
}

public partial class MacroClassConstrainedType<TItem>
    where TItem : class
{
}

public class MyClass
{
}

public partial class MacroBaseClassConstrainedType<TItem>
    where TItem : MyClass
{
}

public partial class MacroNullableBaseClassConstrainedType<TItem>
    where TItem : MyClass?
{
}

public partial class MacroComplexConstrainedType<TItem, TValue>
    where TItem : class?, System.IDisposable, new()
    where TValue : notnull
{
}

public partial class MacroTypeParameterConstrainedType<T, TOther>
    where T : TOther
{
}

public partial class MacroTypeConstraintFixture
{
    [Macro(@"public string TypeConstraintsText => ""$typeConstraints"";")]
    private MacroConstrainedType<int> _value;

    [Macro(@"public string TypeConstraintsNullableText => ""$typeConstraints"";")]
    private MacroNullableConstrainedType<string?> _nullableValue;

    [Macro(@"public string TypeConstraintsClassText => ""$typeConstraints"";")]
    private MacroClassConstrainedType<string> _classValue;

    [Macro(@"public string TypeConstraintsBaseClassText => ""$typeConstraints"";")]
    private MacroBaseClassConstrainedType<MyClass> _baseClassValue;

    [Macro(@"public string TypeConstraintsNullableBaseClassText => ""$typeConstraints"";")]
    private MacroNullableBaseClassConstrainedType<MyClass?> _nullableBaseClassValue;

    [Macro(@"public string TypeConstraintsComplexText => ""$typeConstraints"";")]
    private MacroComplexConstrainedType<MacroComplexDisposable, string> _complexValue;

    [Macro(@"public string TypeConstraintsTypeParameterText => ""$typeConstraints"";")]
    private MacroTypeParameterConstrainedType<string, string> _typeParameterValue;

    [Macro(@"public string TypeConstraintsNonGenericText => ""$typeConstraints"";")]
    private int _plainValue;
}

public sealed class MacroComplexDisposable : System.IDisposable
{
    public void Dispose()
    {
    }
}
