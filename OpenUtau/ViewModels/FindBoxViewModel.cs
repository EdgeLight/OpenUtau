using OpenUtau.Core.Ustx;
using System.Collections.Generic;
using ReactiveUI.Fody.Helpers;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenUtau.App.ViewModels {

    public enum MatchMode {
        Free = 0,
        WholeWord = 1,
        StartsWith = 2,
        EndsWith = 3,
        Regex = 4
    }

    class FindBoxViewModel : ViewModelBase {
        [Reactive] public NotesViewModel? NotesVm { get; set; }
        [Reactive] public bool IsVisible { get; set; }

        private string searchFor;
        [Reactive] public string SearchFor {
            get {
                return searchFor;
            }
            set {
                searchFor = value;
                DiscardSearchResult();
            }
        }
        [Reactive] public string ReplaceTo { get; set; }
        [Reactive] public string MatchCount { get; set; }

        private MatchMode matchMode;
        [Reactive] public MatchMode MatchMode {
            get { 
                return matchMode;
            }
            set {
                matchMode = value;
                DiscardSearchResult();
            } 
        }

        public bool searched = false;
        public List<UNote> searchResults = new List<UNote>();
        public int focusIndex = -1;//当前聚焦的音符在searchResults中的位置，-1表示没有聚焦

        public FindBoxViewModel() {
            IsVisible = false;
            searchFor = "";
            ReplaceTo = "";
            MatchCount = "";
            matchMode = MatchMode.Free;
        }

        //在修改搜索词或修改音符后调用，放弃现有搜索结果
        public void DiscardSearchResult() {
            searched = false;
            focusIndex = -1;
            MatchCount = "";
        }

        public bool IsFocusingNote() {
            return focusIndex >= 0;
        }

        public void UpdateMatchesCount() {
            if (searched) {
                if (IsFocusingNote()) {
                    MatchCount = (focusIndex + 1).ToString() + "/" + searchResults.Count.ToString();
                } else {
                    MatchCount = searchResults.Count.ToString();
                }
            } else {
                MatchCount = "";
            }
        }

        Func<UNote, string, bool> GetMatchFunc() {
            switch (MatchMode) {
                case MatchMode.WholeWord:
                    return (note, searchFor) => note.lyric == searchFor;
                case MatchMode.StartsWith:
                    return (note, searchFor) => note.lyric.StartsWith(searchFor);
                case MatchMode.EndsWith:
                    return (note, searchFor) => note.lyric.EndsWith(searchFor);
                case MatchMode.Regex:
                    return (note, searchFor) => Regex.IsMatch(note.lyric, searchFor);
                default:
                    return (note, searchFor) => note.lyric.Contains(searchFor);
            }
        }

        public void Search() {
            if (SearchFor == "") {
                return;//TODO
            }
            Func<UNote, string, bool> MatchFunc = GetMatchFunc();
            searchResults = NotesVm.Part.notes
                .Where(note => MatchFunc(note, SearchFor))
                .ToList();
            searched = true;
            UpdateMatchesCount();
        }
    }
}
