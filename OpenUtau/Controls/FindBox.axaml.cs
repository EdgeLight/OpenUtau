using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.App.Views;//TODO:可不可以在这里引用？

namespace OpenUtau.App.Controls {
    public partial class FindBox : UserControl {
        private FindBoxViewModel viewModel;
        private bool searched = false;
        private List<UNote> searchResults = new List<UNote>();
        private int focusIndex = -1;//当前聚焦的音符在searchResults中的位置，-1表示没有聚焦

        public FindBox() {
            InitializeComponent();
            DataContext = viewModel = new FindBoxViewModel();
            //box = this.FindControl<TextBox>("PART_Box");
            //listBox = this.FindControl<ListBox>("PART_Suggestions");
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            IsVisible = false;
        }

        public void Show(NotesViewModel notesVm) {
            viewModel.NotesVm = notesVm;
            viewModel.IsVisible = true;
        }

        private bool IsFocusingNote() {
            return this.focusIndex >= 0;
        }

        private UNote GetFocusingNote() {
            return this.searchResults[this.focusIndex];
        }

        private void UpdateMatchesCount() {
            if (this.searched) {
                if (IsFocusingNote()) {
                    viewModel.MatchesCount = (focusIndex+1).ToString() + "/" + searchResults.Count().ToString();
                } else {
                    viewModel.MatchesCount = searchResults.Count().ToString();
                }
            } else {
                viewModel.MatchesCount = "";
            }
        }

        private void Search() {
            string SearchFor = viewModel.SearchFor;
            searchResults = viewModel.NotesVm.Part.notes
                .Where(x => x.lyric.Contains(SearchFor))
                .ToList();
            searched = true;
        }

        //在修改搜索词或修改音符后调用，放弃现有搜索结果
        public void DiscardSearchResult() {
            searched = false;
            focusIndex = -1;
        }

        private void OnFindNext(object? sender, RoutedEventArgs e) {
            FindNext();
        }

        private void FindNext() {
            var NotesVm = viewModel.NotesVm;
            if (NotesVm == null) {
                return;
            }
            //如果还没搜索，则搜索
            if (!searched) {
                Search();
            }
            //获取当前查找位置
            //如果当前已进行了查找，则从当前查找结果位置开始，否则：
            //如果选中了音符，则从选中的第一个音符开始
            //如果没有选中音符，则从音轨开头开始
            if (focusIndex < 0) {
                int searchStartPos = 0;
                if (NotesVm.Selection.Count > 0) {
                    searchStartPos = NotesVm.Selection.FirstOrDefault().position - 1;
                    //确保当前选择的音符被查找到。OpenUTAU音符输入的时间精度为5tick，一般不会出现1tick的音符长度。
                }
                //从搜索位置开始查找，并跳转
                //UNote的比较优先比较位置，如果位置相同则比较hash，只有完全相同的UNote才相等
                var index = searchResults.BinarySearch(new UNote { position = searchStartPos });
                if (index < 0) {
                    index = ~index;//一般不存在相等匹配，则返回值为目标区间右端索引的按位反（负整数）
                }
                focusIndex = index;
            } else {
                focusIndex++;
            }
            //如果到最后一个音符，则弹窗
            if (focusIndex >= searchResults.Count()) {
                //TODO:弹窗
                /*MessageBox.Show(
                    this,
                    "已到达当前区段结尾",//ThemeManager.GetString("dialogs.export.savefirst"),
                    ThemeManager.GetString("errors.caption"),
                    MessageBox.MessageBoxButtons.Ok);*/
                focusIndex = 0;
            }
            //跳转
            DocManager.Inst.ExecuteCmd(new FocusNoteNotification(NotesVm.Part, searchResults[focusIndex]));
            UpdateMatchesCount();
        }

        private void OnFindPrev(object? sender, RoutedEventArgs e) {
            FindPrev();
        }

        private void FindPrev() {
            var NotesVm = viewModel.NotesVm;
            if (NotesVm == null) {
                return;
            }
            //如果还没搜索，则搜索
            if (!searched) {
                Search();
            }
            //获取当前查找位置
            //如果当前已进行了查找，则从当前查找结果位置开始，否则：
            //如果选中了音符，则从选中的第一个音符开始
            //如果没有选中音符，则从音轨开头开始
            if (focusIndex < 0) {
                int searchStartPos = NotesVm.Part.Duration;
                if (NotesVm.Selection.Count > 0) {
                    searchStartPos = NotesVm.Selection.LastOrDefault().position + 1;
                    //确保当前选择的音符被查找到。OpenUTAU音符输入的时间精度为5tick，一般不会出现1tick的音符长度。
                }
                //从搜索位置开始查找，并跳转
                //UNote的比较优先比较位置，如果位置相同则比较hash，只有完全相同的UNote才相等
                var index = searchResults.BinarySearch(new UNote { position = searchStartPos });
                if (index < 0) {
                    index = ~index;//一般不存在相等匹配，则返回值为目标区间右端索引的按位反（负整数）
                }
                focusIndex = index;
            } else {
                focusIndex--;
            }
            //如果到第一个音符，则弹窗
            if (focusIndex < 0) {
                //TODO:弹窗
                /*MessageBox.Show(
                    this,
                    "已到达当前区段开头",//ThemeManager.GetString("dialogs.export.savefirst"),
                    ThemeManager.GetString("errors.caption"),
                    MessageBox.MessageBoxButtons.Ok);*/
                focusIndex = searchResults.Count() - 1;
            }
            //跳转
            DocManager.Inst.ExecuteCmd(new FocusNoteNotification(NotesVm.Part, searchResults[focusIndex]));
            UpdateMatchesCount();
        }

        private void OnReplaceOne(object? sender, RoutedEventArgs e) {
            //TODO:当前没有搜索音符时，怎么办？
            if (IsFocusingNote()) {
                UNote note = GetFocusingNote();
                FindNext();
                DocManager.Inst.StartUndoGroup();
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(
                    viewModel.NotesVm.Part,
                    GetFocusingNote(),
                    note.lyric.Replace(viewModel.SearchFor,viewModel.ReplaceTo)));
                DocManager.Inst.EndUndoGroup();
            }
            //TODO:目前每次替换都要重新搜索，能不能免去？
        }
        private void OnReplaceAll(object? sender, RoutedEventArgs e) {
            var Part = viewModel.NotesVm.Part;
            var SearchFor = viewModel.SearchFor;
            var ReplaceTo = viewModel.ReplaceTo;
            DocManager.Inst.StartUndoGroup();
            foreach (UNote note in searchResults) {
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(
                    Part,
                    GetFocusingNote(),
                    note.lyric.Replace(SearchFor, ReplaceTo))); 
            }
            DocManager.Inst.EndUndoGroup();
            Search();
        }
        private void OnClose(object? sender, RoutedEventArgs e) {
            viewModel.IsVisible = false;
        }
    }
}
