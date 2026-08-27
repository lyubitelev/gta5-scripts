using System;
using System.Collections.Generic;

namespace gta.Core
{
    public sealed class MenuNavigationState<TCategory>
    {
        public TCategory Category { get; set; }
        public int Index { get; set; }
        public int Page { get; set; }

        public MenuNavigationState(TCategory category, int index = 0, int page = 0)
        {
            Category = category;
            Index = index;
            Page = page;
        }
    }

    public sealed class MenuNavigator<TCategory>
    {
        private readonly Stack<MenuNavigationState<TCategory>> _history = new Stack<MenuNavigationState<TCategory>>();
        private readonly Dictionary<TCategory, int> _savedIndexes = new Dictionary<TCategory, int>();
        private readonly Dictionary<TCategory, int> _savedPages = new Dictionary<TCategory, int>();
        private readonly TCategory _rootCategory;

        public TCategory CurrentCategory { get; private set; }

        private int _currentIndex;
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                _currentIndex = value;
                _savedIndexes[CurrentCategory] = value;
            }
        }

        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                _savedPages[CurrentCategory] = value;
            }
        }

        public MenuNavigator(TCategory rootCategory)
        {
            _rootCategory = rootCategory;
            CurrentCategory = rootCategory;
            _currentIndex = 0;
            _currentPage = 0;
        }

        public bool IsAtRoot => EqualityComparer<TCategory>.Default.Equals(CurrentCategory, _rootCategory);
        public bool CanGoBack => _history.Count > 0;
        public int Depth => _history.Count;

        public void NavigateTo(TCategory newCategory, bool pushHistory = true)
        {
            // Save state of category we are leaving
            _savedIndexes[CurrentCategory] = _currentIndex;
            _savedPages[CurrentCategory] = _currentPage;

            if (pushHistory)
            {
                _history.Push(new MenuNavigationState<TCategory>(CurrentCategory, _currentIndex, _currentPage));
            }

            CurrentCategory = newCategory;
            // Restore saved index & page for the target category, or 0
            _currentIndex = _savedIndexes.TryGetValue(newCategory, out int idx) ? idx : 0;
            _currentPage = _savedPages.TryGetValue(newCategory, out int pg) ? pg : 0;
        }

        public bool GoBack()
        {
            if (_history.Count == 0)
            {
                return false;
            }

            // Save state of category we are leaving
            _savedIndexes[CurrentCategory] = _currentIndex;
            _savedPages[CurrentCategory] = _currentPage;

            var previous = _history.Pop();
            CurrentCategory = previous.Category;
            _currentIndex = previous.Index;
            _currentPage = previous.Page;
            _savedIndexes[CurrentCategory] = _currentIndex;
            _savedPages[CurrentCategory] = _currentPage;
            return true;
        }

        public void Reset(bool keepMemory = true)
        {
            if (!keepMemory)
            {
                _savedIndexes.Clear();
                _savedPages.Clear();
            }
            _history.Clear();
            CurrentCategory = _rootCategory;
            _currentIndex = _savedIndexes.TryGetValue(_rootCategory, out int idx) ? idx : 0;
            _currentPage = _savedPages.TryGetValue(_rootCategory, out int pg) ? pg : 0;
        }

        public void SaveCurrentState()
        {
            _savedIndexes[CurrentCategory] = _currentIndex;
            _savedPages[CurrentCategory] = _currentPage;
        }

        public void ClampIndex(int itemCount)
        {
            if (itemCount <= 0)
            {
                CurrentIndex = 0;
                return;
            }

            if (CurrentIndex < 0) CurrentIndex = 0;
            if (CurrentIndex >= itemCount) CurrentIndex = itemCount - 1;
        }

        public void MoveNext(int itemCount)
        {
            if (itemCount <= 0) return;
            CurrentIndex = (CurrentIndex + 1) % itemCount;
        }

        public void MovePrevious(int itemCount)
        {
            if (itemCount <= 0) return;
            CurrentIndex = (CurrentIndex - 1 + itemCount) % itemCount;
        }
    }
}
