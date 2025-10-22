using System.Collections.Generic;
using static CitizenFX.Core.Native.Function;
using static CitizenFX.Core.Native.API;

namespace MenuAPI
{
    public class MenuListItem : MenuItem
    {
#if FIVEM
        private const int LabelSliderVisibleCharacters = 26;
#else
        private const int LabelSliderVisibleCharacters = 20;
#endif
        private const int LabelSliderScrollInterval = 200;
        private const int LabelSliderInitialPause = 800;
        private const int LabelSliderLoopPause = 1200;
        private const int LabelSliderInactivityReset = 1500;

        private int _lastListIndex = -1;
        private int _sliderStartIndex = 0;
        private int _sliderLastUpdate = 0;
        private int _sliderPauseUntil = 0;
        private string _sliderSourceText = string.Empty;
        private int _lastDrawTick = 0;
        private List<string> listItems = new List<string>();

        public int ListIndex { get; set; } = 0;
        public List<string> ListItems
        {
            get => listItems;
            set
            {
                listItems = value ?? new List<string>();
                ResetCachedSlider();
            }
        }
        public bool HideArrowsWhenNotSelected { get; set; } = false;
        public bool ShowOpacityPanel { get; set; } = false;
        public bool ShowColorPanel { get; set; } = false;
        public ColorPanelType ColorPanelColorType = ColorPanelType.Hair;
        public enum ColorPanelType
        {
            Hair,
            Makeup
        }
        public int ItemsCount => ListItems.Count;

        public string GetCurrentSelection()
        {
            if (ItemsCount > 0 && ListIndex >= 0 && ListIndex < ItemsCount)
            {
                return ListItems[ListIndex];
            }
            return null;
        }

        public MenuListItem(string text, List<string> items, int index) : this(text, items, index, null) { }
        public MenuListItem(string text, List<string> items, int index, string description) : base(text, description)
        {
            ListItems = items;
            ListIndex = index;
        }

        internal override void Draw(int indexOffset)
        {
            if (ItemsCount < 1)
            {
                // Add a dummy item to prevent the other while loops from freezing the game.
                ListItems.Add("N/A");
            }

            while (ListIndex < 0)
            {
                ListIndex += ItemsCount;
            }

            while (ListIndex >= ItemsCount)
            {
                ListIndex -= ItemsCount;
            }

            string currentSelection = GetCurrentSelection() ?? "~r~N/A~s~";
            int currentTick = GetGameTimer();

            if (_lastDrawTick != 0 && currentTick - _lastDrawTick > LabelSliderInactivityReset)
            {
                ResetCachedSlider();
                // Ensure we restart using the current selection value below.
            }

            if (_lastListIndex != ListIndex || _sliderSourceText != currentSelection)
            {
                ResetSliderState(currentSelection, ShouldScroll(currentSelection), currentTick);
                _lastListIndex = ListIndex;
            }

            string displayValue = GetScrollableSelection(currentSelection, currentTick);

            if (HideArrowsWhenNotSelected && !Selected)
            {
                Label = displayValue;
            }
            else
            {
                Label = $"~s~← {displayValue} ~s~→";
            }

            base.Draw(indexOffset);
            _lastDrawTick = currentTick;
        }

        internal override void GoRight()
        {
            if (ItemsCount > 0)
            {
                int oldIndex = ListIndex;
                int newIndex = oldIndex;
                if (ListIndex >= ItemsCount - 1)
                {
                    newIndex = 0;
                }
                else
                {
                    newIndex++;
                }
                ListIndex = newIndex;
                string selection = GetCurrentSelection() ?? string.Empty;
                ResetSliderState(selection, ShouldScroll(selection), GetGameTimer());
                ParentMenu.ListItemIndexChangeEvent(ParentMenu, this, oldIndex, newIndex, Index);
#if FIVEM
                PlaySoundFrontend(-1, "NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
#endif
#if REDM
                // Has invalid parameter types in API.
                Call((CitizenFX.Core.Native.Hash)0xCE5D0FFE83939AF1, -1, "NAV_RIGHT", "HUD_SHOP_SOUNDSET", 1);
#endif
            }
        }

        internal override void GoLeft()
        {
            if (ItemsCount > 0)
            {
                int oldIndex = ListIndex;
                int newIndex = oldIndex;
                if (ListIndex < 1)
                {
                    newIndex = ItemsCount - 1;
                }
                else
                {
                    newIndex--;
                }
                ListIndex = newIndex;
                string selection = GetCurrentSelection() ?? string.Empty;
                ResetSliderState(selection, ShouldScroll(selection), GetGameTimer());

                ParentMenu.ListItemIndexChangeEvent(ParentMenu, this, oldIndex, newIndex, Index);
#if FIVEM
                PlaySoundFrontend(-1, "NAV_LEFT_RIGHT", "HUD_FRONTEND_DEFAULT_SOUNDSET", false);
#endif
#if REDM
                // Has invalid parameter types in API.
                Call((CitizenFX.Core.Native.Hash)0xCE5D0FFE83939AF1, -1, "NAV_LEFT", "HUD_SHOP_SOUNDSET", 1);
#endif
            }
        }

        internal override void Select()
        {
            ParentMenu.ListItemSelectEvent(ParentMenu, this, ListIndex, Index);
        }

        private string GetScrollableSelection(string text, int currentTick)
        {
            if (!ShouldScroll(text))
            {
                return text;
            }

            if (_sliderPauseUntil == 0)
            {
                _sliderPauseUntil = currentTick + LabelSliderInitialPause;
            }

            if (currentTick >= _sliderPauseUntil &&
                (_sliderLastUpdate == 0 || currentTick - _sliderLastUpdate >= LabelSliderScrollInterval))
            {
                _sliderStartIndex = (_sliderStartIndex + 1) % text.Length;
                _sliderLastUpdate = currentTick;

                if (_sliderStartIndex == 0)
                {
                    _sliderPauseUntil = currentTick + LabelSliderLoopPause;
                }
            }

            return GetSliderWindow(text);
        }

        private string GetSliderWindow(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.Length <= LabelSliderVisibleCharacters)
            {
                return text;
            }

            int remainingLength = text.Length - _sliderStartIndex;
            if (remainingLength >= LabelSliderVisibleCharacters)
            {
                return text.Substring(_sliderStartIndex, LabelSliderVisibleCharacters);
            }

            string firstPart = text.Substring(_sliderStartIndex, remainingLength);
            string secondPart = text.Substring(0, LabelSliderVisibleCharacters - remainingLength);
            return firstPart + secondPart;
        }

        private bool ShouldScroll(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.IndexOf('~') != -1 || text.IndexOf('\n') != -1 || text.IndexOf('\r') != -1)
            {
                return false;
            }

            return text.Length > LabelSliderVisibleCharacters;
        }

        private void ResetSliderState(string text, bool enableScroll, int currentTick)
        {
            _sliderSourceText = text ?? string.Empty;
            _sliderStartIndex = 0;
            if (enableScroll)
            {
                _sliderLastUpdate = currentTick;
                _sliderPauseUntil = currentTick + LabelSliderInitialPause;
            }
            else
            {
                _sliderLastUpdate = 0;
                _sliderPauseUntil = 0;
            }
        }

        private void ResetCachedSlider()
        {
            _sliderSourceText = string.Empty;
            _sliderStartIndex = 0;
            _sliderLastUpdate = 0;
            _sliderPauseUntil = 0;
            _lastListIndex = -1;
            _lastDrawTick = 0;
        }
    }
}
