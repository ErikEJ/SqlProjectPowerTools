using System.Windows;
using System.Windows.Controls;
using DacFXToolLib.Common;

namespace SqlProjectsPowerTools
{
    public partial class SchemaCompareDialog
    {
        private readonly VisualCompareResult compareResult;

        public SchemaCompareDialog(VisualCompareResult result, string projectName)
        {
            compareResult = result ?? throw new ArgumentNullException(nameof(result));

            InitializeComponent();

            SourceLabel.Text = projectName;
            DeploymentScriptBox.Text = result.DeploymentScript ?? string.Empty;

            var differences = result.Differences ?? [];
            DifferencesGrid.ItemsSource = differences;

            StatusLabel.Text = differences.Length == 0
                ? "No differences found."
                : $"{differences.Length} difference(s) found.";
        }

        private void DifferencesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DifferencesGrid.SelectedItem is SchemaDifferenceModel selected)
            {
                SourceScriptBox.Text = selected.SourceScript ?? string.Empty;
                TargetScriptBox.Text = selected.TargetScript ?? string.Empty;
            }
            else
            {
                SourceScriptBox.Text = string.Empty;
                TargetScriptBox.Text = string.Empty;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
