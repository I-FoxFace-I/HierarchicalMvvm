using HierarchicalMvvm.Demo.ViewModels;
using System.Windows;

namespace HierarchicalMvvm.Demo
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}