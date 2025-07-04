//using Xunit;
//using Xunit.Abstractions;

//namespace HierarchicalMvvm.Tests
//{
//    public class TestRunner
//    {
//        private readonly ITestOutputHelper _output;

//        public TestRunner(ITestOutputHelper output)
//        {
//            _output = output;
//        }

//        [Fact]
//        public void RunAllTests()
//        {
//            _output.WriteLine("🧪 Starting Hierarchical MVVM Test Suite");
//            _output.WriteLine("=".PadRight(50, '='));
//            _output.WriteLine("✅ Source Generator Tests");
//            _output.WriteLine("✅ Memory Management Tests");  
//            _output.WriteLine("✅ Event Propagation Tests");
//            _output.WriteLine("✅ Conversion Tests");
//            _output.WriteLine("✅ Integration Tests");
//            _output.WriteLine("=".PadRight(50, '='));
//            _output.WriteLine("🎉 All tests should pass!");
//        }
//    }
//}