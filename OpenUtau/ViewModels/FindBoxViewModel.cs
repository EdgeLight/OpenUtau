
using System.Collections.Generic;
using System.Windows.Documents;
using OpenUtau.Core.Ustx;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    class FindBoxViewModel : ViewModelBase {
        [Reactive] public NotesViewModel? NotesVm { get; set; }
        [Reactive] public bool IsVisible { get; set; }
        [Reactive] public string SearchFor { get; set; }
        [Reactive] public string ReplaceTo { get; set; }
        [Reactive] public string MatchesCount { get; set; }

        public FindBoxViewModel() {
            IsVisible = false;
            SearchFor = "";
            ReplaceTo = "";
            MatchesCount = "";
        }
    }
}
