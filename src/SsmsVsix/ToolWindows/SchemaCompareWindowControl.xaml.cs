using System.Windows;
using System.Windows.Controls;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public partial class SchemaCompareWindowControl : UserControl
    {
        public SchemaCompareWindowControl()
        {
            InitializeComponent();
            DataContext = new SchemaCompareViewModel();
        }

        public void SetResult(VisualCompareResult result, string projectName, string targetName)
        {
            if (DataContext is SchemaCompareViewModel vm)
            {
                vm.LoadResult(result, projectName, targetName);
            }
        }

        private void CopyDeploymentScript_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SchemaCompareViewModel vm && !string.IsNullOrEmpty(vm.DeploymentScript))
            {
                Clipboard.SetText(vm.DeploymentScript);
            }
        }
    }
}
