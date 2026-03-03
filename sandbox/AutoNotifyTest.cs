namespace FGenerator.Sandbox;

/// <summary>
///     Example class that uses the AutoNotify generator.
///     The [AutoNotify] attribute triggers code generation.
///     The class must be partial to allow the generator to extend it.
/// </summary>
[AutoNotify]
public class Person<T>
{
    private int _age;

    // Private fields - the generator will create properties for these
    private readonly string _firstName = string.Empty;
    private readonly string _lastName = string.Empty;

    // You can add your own properties and methods
    public string FullName => $"{_firstName} {_lastName}";

    public void CelebrateBirthday()
    {
        // Use the generated SetField method to update with notification
        SetField(ref _age, _age + 1);
    }
}

// TEST: nested version
public partial class Container
{
    [AutoNotify]
    public class PersonNested<T>
    {
        private readonly string _firstName = string.Empty;
        private readonly string _lastName = string.Empty;
        public string FullName => $"{_firstName} {_lastName}";
    }
}