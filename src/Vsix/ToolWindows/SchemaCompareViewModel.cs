using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DacFXToolLib.Common;
using DiffPlex;
using DiffPlex.DiffBuilder;

namespace SqlProjectsPowerTools
{
    internal sealed class SchemaCompareViewModel : INotifyPropertyChanged
    {
        private readonly SideBySideDiffBuilder diffBuilder = new SideBySideDiffBuilder(new Differ());
        private string sourceDisplay = string.Empty;
        private string targetDisplay = string.Empty;
        private string status = "Run a Visual Schema Compare to see results.";
        private string deploymentScript = string.Empty;
        private bool isBusy;
        private SchemaDifferenceModel selectedDifference;

        public SchemaCompareViewModel()
        {
            Differences = new ObservableCollection<SchemaDifferenceModel>();
            SourceDiffLines = new ObservableCollection<DiffLineViewModel>();
            TargetDiffLines = new ObservableCollection<DiffLineViewModel>();
        }

        public ObservableCollection<SchemaDifferenceModel> Differences { get; }

        public ObservableCollection<DiffLineViewModel> SourceDiffLines { get; }

        public ObservableCollection<DiffLineViewModel> TargetDiffLines { get; }

        public string SourceDisplay
        {
            get => sourceDisplay;
            private set
            {
                sourceDisplay = value;
                OnPropertyChanged();
            }
        }

        public string TargetDisplay
        {
            get => targetDisplay;
            private set
            {
                targetDisplay = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        public string DeploymentScript
        {
            get => deploymentScript;
            private set
            {
                deploymentScript = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => isBusy;
            set
            {
                isBusy = value;
                OnPropertyChanged();
            }
        }

        public SchemaDifferenceModel SelectedDifference
        {
            get => selectedDifference;
            set
            {
                selectedDifference = value;
                OnPropertyChanged();
                UpdateDiffView();
            }
        }

        public void LoadResult(VisualCompareResult result, string projectName, string targetName)
        {
            SourceDisplay = projectName;
            TargetDisplay = targetName;

            Differences.Clear();
            SourceDiffLines.Clear();
            TargetDiffLines.Clear();
            SelectedDifference = null;

            var diffs = result?.Differences ?? new List<SchemaDifferenceModel>();
            foreach (var diff in diffs)
            {
                Differences.Add(diff);
            }

            DeploymentScript = result?.DeploymentScript ?? string.Empty;

            Status = diffs.Count == 0
                ? "No differences found."
                : $"{diffs.Count} difference(s) found.";
        }

        private void UpdateDiffView()
        {
            SourceDiffLines.Clear();
            TargetDiffLines.Clear();

            if (selectedDifference == null)
            {
                return;
            }

            var sourceText = selectedDifference.SourceScript ?? string.Empty;
            var targetText = selectedDifference.TargetScript ?? string.Empty;

            var diffResult = diffBuilder.BuildDiffModel(sourceText, targetText);

            foreach (var line in diffResult.OldText.Lines)
            {
                SourceDiffLines.Add(new DiffLineViewModel(line));
            }

            foreach (var line in diffResult.NewText.Lines)
            {
                TargetDiffLines.Add(new DiffLineViewModel(line));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
