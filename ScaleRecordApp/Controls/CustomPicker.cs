using CommunityToolkit.Maui.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ScaleRecordApp.Controls
{
    public class CustomPicker : Picker
    {
        //public static readonly BindableProperty ExtraCommandProperty =
        //    BindableProperty.Create(nameof(ExtraCommand), typeof(ICommand), typeof(CustomPicker));

        //public static readonly BindableProperty AddButtonTextProperty =
        //    BindableProperty.Create(nameof(AddButtonText), typeof(string), typeof(CustomPicker), "+ Добавить...");

        //public static readonly BindableProperty CancelButtonTextProperty =
        //    BindableProperty.Create(nameof(CancelButtonText), typeof(string), typeof(CustomPicker), "Отмена");

        ///// <summary>Команда для кнопки "+ Добавить …" в попапе (задаётся из VM, где находится Picker).</summary>
        //public ICommand ExtraCommand
        //{
        //    get => (ICommand)GetValue(ExtraCommandProperty);
        //    set => SetValue(ExtraCommandProperty, value);
        //}

        //public string AddButtonText
        //{
        //    get => (string)GetValue(AddButtonTextProperty);
        //    set => SetValue(AddButtonTextProperty, value);
        //}

        //public string CancelButtonText
        //{
        //    get => (string)GetValue(CancelButtonTextProperty);
        //    set => SetValue(CancelButtonTextProperty, value);
        //}

        //public CustomPicker()
        //{
        //    // Перехватываем фокус, мгновенно снимаем его и открываем свой Popup.
        //    Focused += async (s, e) =>
        //    {
        //        Unfocus(); // предотвращаем открытие нативного списка
        //        var popup = new CustomPickerPopup(this);
        //        await Application.Current?.MainPage?.ShowPopupAsync(popup);
        //    };
        //}

        ////internal void CommitSelection(object item)
        ////{
        ////    SelectedItem = item; // для Picker с ItemsSource этого достаточно
        ////}
        //internal void CommitSelection(object item)
        //{
        //    SelectedItem = item;

        //    if (ItemsSource is IList list)
        //    {
        //        var index = list.IndexOf(item);
        //        if (index >= 0)
        //            SelectedIndex = index; // 👈 заставляем Picker отобразить выбранный элемент
        //    }
        //}



        public static readonly BindableProperty ExtraCommandProperty =
       BindableProperty.Create(nameof(ExtraCommand), typeof(ICommand), typeof(CustomPicker));

        public static readonly BindableProperty AddButtonTextProperty =
            BindableProperty.Create(nameof(AddButtonText), typeof(string), typeof(CustomPicker), "Добавить / изменить список");

        public static readonly BindableProperty CancelButtonTextProperty =
            BindableProperty.Create(nameof(CancelButtonText), typeof(string), typeof(CustomPicker), "Отмена");

        public ICommand ExtraCommand
        {
            get => (ICommand)GetValue(ExtraCommandProperty);
            set => SetValue(ExtraCommandProperty, value);
        }

        public string AddButtonText
        {
            get => (string)GetValue(AddButtonTextProperty);
            set => SetValue(AddButtonTextProperty, value);
        }

        public string CancelButtonText
        {
            get => (string)GetValue(CancelButtonTextProperty);
            set => SetValue(CancelButtonTextProperty, value);
        }

        public CustomPicker()
        {
            // Отключаем стандартное открытие
            IsEnabled = true;

            // Ловим тап по самому контролу
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                var popup = new CustomPickerPopup(this);
                await Application.Current?.MainPage?.ShowPopupAsync(popup);
            };
            GestureRecognizers.Add(tap);
        }

        //internal void CommitSelection(object item)
        //{
        //    SelectedItem = item;

        //    if (ItemsSource is IList list)
        //    {
        //        var index = list.IndexOf(item);
        //        if (index >= 0)
        //            SelectedIndex = index;
        //    }
        //}


        //internal void CommitSelection(object item)
        //{
        //    SelectedItem = item;

        //    if (ItemsSource != null)
        //    {
        //        int index = -1;
        //        int i = 0;
        //        foreach (var obj in ItemsSource)
        //        {
        //            if (Equals(obj, item))
        //            {
        //                index = i;
        //                break;
        //            }
        //            i++;
        //        }

        //        if (index >= 0)
        //            SelectedIndex = index; // обязательно выставляем индекс!
        //        else
        //            SelectedIndex = -1;
        //    }
        //    else
        //    {
        //        SelectedIndex = -1;
        //    }
        //}

        internal void CommitSelection(object item)
        {
            if (ItemsSource is null)
                return;

            // Находим индекс выбранного объекта в ItemsSource (работает и с IEnumerable)
            var index = -1;
            var i = 0;
            foreach (var obj in ItemsSource)
            {
                if (ReferenceEquals(obj, item) || Equals(obj, item))
                {
                    index = i;
                    break;
                }
                i++;
            }

            // ВАЖНО: ставим именно SelectedIndex — тогда MAUI сам проставит SelectedItem и перерисует текст
            SelectedIndex = index;

            // На всякий случай “пнуть” разметку (на некоторых сборках Android это помогает)
            InvalidateMeasure();
        }


    }
}
