using AutoNotifyGenerator;

namespace FGenerator.Sandbox
{
    /// <summary>
    /// Example class that uses the AutoNotify generator.
    /// The [AutoNotify] attribute triggers code generation.
    /// The class must be partial to allow the generator to extend it.
    /// </summary>
    [AutoNotify]
    public partial class Person<T>
    {
        // Private fields - the generator will create properties for these
        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private int _age;

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
        public partial class PersonNested<T>
        {
            private string _firstName = string.Empty;
            private string _lastName = string.Empty;
            public string FullName => $"{_firstName} {_lastName}";
        }
    }
}
