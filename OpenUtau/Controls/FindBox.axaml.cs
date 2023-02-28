using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using OpenUtau.App.Views;//TODO:可不可以在这里引用？

namespace OpenUtau.App.Controls {
    public partial class FindBox : UserControl {
        private FindBoxViewModel viewModel;
        private TextBox SearchFor_Box;
        private TextBox ReplaceTo_Box;

        public FindBox() {
            InitializeComponent();
            DataContext = viewModel = new FindBoxViewModel();
            SearchFor_Box = this.FindControl<TextBox>("SearchFor_Box");
            ReplaceTo_Box = this.FindControl<TextBox>("ReplaceTo_Box");
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
            IsVisible = false;
        }

        public void Show(NotesViewModel notesVm) {
            viewModel.NotesVm = notesVm;
            viewModel.IsVisible = true;
        }

        public bool IsInputing() {
            return viewModel.IsVisible && 
                (SearchFor_Box.IsFocused || ReplaceTo_Box.IsFocused);
        }

        private bool IsFocusingNote() {
            return viewModel.focusIndex >= 0;
        }

        private UNote GetFocusingNote() {
            return viewModel.searchResults[viewModel.focusIndex];
        }

        private void OnFindNext(object? sender, RoutedEventArgs e) {
            FindNext();
        }

        private void SearchForBox_KeyDown(object? sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                FindNext();
            }
        }

        private void FindNext() {
            var NotesVm = viewModel.NotesVm;
            if (NotesVm == null) {
                return;
            }
            //如果还没搜索，则搜索
            if (!viewModel.searched) {
                viewModel.Search();
            }
            //如果搜索结果为空，则退出
            if (viewModel.searchResults.Count() == 0) {
                return;
            }
            //获取当前查找位置
            //如果当前已进行了查找，则从当前查找结果位置开始，否则：
            //如果选中了音符，则从选中的第一个音符开始
            //如果没有选中音符，则从音轨开头开始
            if (viewModel.focusIndex < 0) {
                int searchStartPos = 0;
                if (NotesVm.Selection.Count > 0) {
                    searchStartPos = NotesVm.Selection.FirstOrDefault().position - 1;
                    //确保当前选择的音符被查找到。OpenUTAU音符输入的时间精度为5tick，一般不会出现1tick的音符长度。
                }
                //从搜索位置开始查找，并跳转
                //UNote的比较优先比较位置，如果位置相同则比较hash，只有完全相同的UNote才相等
                var index = viewModel.searchResults.BinarySearch(new UNote { position = searchStartPos });
                if (index < 0) {
                    index = ~index;//一般不存在相等匹配，则返回值为目标区间右端索引的按位反（负整数）
                }
                viewModel.focusIndex = index;
            } else {
                viewModel.focusIndex++;
            }
            //如果到最后一个音符，则弹窗
            if (viewModel.focusIndex >= viewModel.searchResults.Count()) {
                //TODO:弹窗
                /*MessageBox.Show(
                    this,
                    "已到达当前区段结尾",//ThemeManager.GetString("dialogs.export.savefirst"),
                    ThemeManager.GetString("errors.caption"),
                    MessageBox.MessageBoxButtons.Ok);*/
                viewModel.focusIndex = 0;
            }
            //跳转
            DocManager.Inst.ExecuteCmd(
                new FocusNoteNotification(NotesVm.Part, viewModel.searchResults[viewModel.focusIndex])
                );
            viewModel.UpdateMatchesCount();
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
            if (!viewModel.searched) {
                viewModel.Search();
            }
            //如果搜索结果为空，则退出
            if(viewModel.searchResults.Count() == 0) {
                return;
            }
            //获取当前查找位置
            //如果当前已进行了查找，则从当前查找结果位置开始，否则：
            //如果选中了音符，则从选中的第一个音符开始
            //如果没有选中音符，则从音轨开头开始
            if (viewModel.focusIndex < 0) {
                int searchStartPos = NotesVm.Part.Duration;
                if (NotesVm.Selection.Count > 0) {
                    searchStartPos = NotesVm.Selection.LastOrDefault().position + 1;
                    //确保当前选择的音符被查找到。OpenUTAU音符输入的时间精度为5tick，一般不会出现1tick的音符长度。
                }
                //从搜索位置开始查找，并跳转
                //UNote的比较优先比较位置，如果位置相同则比较hash，只有完全相同的UNote才相等
                var index = viewModel.searchResults.BinarySearch(new UNote { position = searchStartPos });
                if (index < 0) {
                    index = ~index;//一般不存在相等匹配，则返回值为目标区间右端索引的按位反（负整数）
                }
                viewModel.focusIndex = index;
            } else {
                viewModel.focusIndex--;
            }
            //如果到第一个音符，则弹窗
            if (viewModel.focusIndex < 0) {
                //TODO:弹窗
                /*MessageBox.Show(
                    this,
                    "已到达当前区段开头",//ThemeManager.GetString("dialogs.export.savefirst"),
                    ThemeManager.GetString("errors.caption"),
                    MessageBox.MessageBoxButtons.Ok);*/
                viewModel.focusIndex = viewModel.searchResults.Count() - 1;
            }
            //跳转
            DocManager.Inst.ExecuteCmd(
                new FocusNoteNotification(NotesVm.Part, viewModel.searchResults[viewModel.focusIndex])
                );
            viewModel.UpdateMatchesCount();
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
            foreach (UNote note in viewModel.searchResults) {
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(
                    Part,
                    GetFocusingNote(),
                    note.lyric.Replace(SearchFor, ReplaceTo))); 
            }
            DocManager.Inst.EndUndoGroup();
            viewModel.Search();
        }
        private void OnClose(object? sender, RoutedEventArgs e) {
            viewModel.IsVisible = false;
        }
    }
}
