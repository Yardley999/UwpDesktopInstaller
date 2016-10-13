using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace App1
{
    public class PopContainner
    {
        private Frame _hostFrame = null;
        //private Popup _Popup = new Popup();
        TransitionCollection trans = new TransitionCollection() { new PopupThemeTransition() };
        private Grid _PopContent = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch};
        private FrameworkElement _element = null;
        public delegate void LoadedEventHandler();
        public event LoadedEventHandler OnLoaded;
        const double time = 0.4d;


        public PopContainner(FrameworkElement element)
        {
            _element = element;
            SetFrame(Window.Current.Content as Frame);
            _PopContent.Loaded += _Popup_Loaded;
            _PopContent.Transitions = trans;
        }

        public PopContainner(FrameworkElement element, bool autoFade)
        {
            _element = element;
            if (autoFade)
            {
                _PopContent.Background = new SolidColorBrush(Colors.Transparent);
                _PopContent.Tapped += (s, arg) => { Hide(); };
            }
            SetFrame(Window.Current.Content as Frame);
            _PopContent.Loaded += _Popup_Loaded;
            _PopContent.Transitions = trans;
        }

        private void _Popup_Loaded(object sender, RoutedEventArgs e)
        {
            OnLoaded?.Invoke();
        }

        public static PopContainner Show(FrameworkElement element, bool autoFade = false)
        {
            var popC = new PopContainner(element, autoFade);
            popC.Show();

            return popC;
        }

        public void Show()
        {
            _PopContent.Children.Clear();

            _PopContent.Children.Add(_element);

            _element.HorizontalAlignment = HorizontalAlignment.Stretch;
            _element.HorizontalAlignment = HorizontalAlignment.Stretch;

            //_Popup.Child = _PopContent;
            //_Popup.IsOpen = true;
            var homeGrid = (_hostFrame.Content as Page).Content as Grid;
            homeGrid.Children.Add(_PopContent);
            homeGrid.SetValue(Grid.RowProperty, 0);
            homeGrid.SetValue(Grid.ColumnProperty, 0);
            homeGrid.SetValue(Grid.RowSpanProperty, 10);
            homeGrid.SetValue(Grid.ColumnProperty, 10);
        }

        public void Hide()
        {
            _PopContent.Children.Clear();
            //_Popup.IsOpen = false;
            if (_PopContent.Parent != null)
            {
                (_PopContent.Parent as Grid).Children.Remove(_PopContent);
            }
        }

        public void SetBackground(SolidColorBrush bgColor)
        {
            _PopContent.Background = bgColor;
        }

        public void SetFrame(Frame frame)
        {
            _hostFrame = frame;
            //_Popup.Width = _PopContent.Width = _hostFrame.ActualWidth;
            //_Popup.Height = _PopContent.Height = _hostFrame.ActualHeight;
            //_hostFrame.SizeChanged += _hostFrame_SizeChanged;
        }

        /// <summary>
        /// Make sure the loading frame is full screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _hostFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _PopContent.Width = _hostFrame.ActualWidth;
            _PopContent.Height = _hostFrame.ActualHeight;
        }
    }
}
