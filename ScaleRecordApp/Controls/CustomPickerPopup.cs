using CommunityToolkit.Maui.Views;


namespace ScaleRecordApp.Controls
{
    public class CustomPickerPopup : Popup
    {
        public CustomPickerPopup(CustomPicker parent)
        {
            CanBeDismissedByTappingOutsideOfPopup = true;

            // Верхняя панель с кнопкой "+ Добавить …" справа
            var addButton = new Button
            {
                Text = parent.AddButtonText,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            addButton.SetBinding(Button.CommandProperty, new Binding(nameof(CustomPicker.ExtraCommand), source: parent));

            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };
            // Левая «пустышка» для выравнивания
            header.Children.Add(new Label { Text = string.Empty, IsVisible = false });
            header.Children.Add(addButton);

            // Список выбора
            var list = new CollectionView
            {
                SelectionMode = SelectionMode.Single,
                WidthRequest = 700,
                HeightRequest = 300
                
            };

            // Привязка ItemsSource из родительского Picker (чтобы список обновлялся при изменении)
            list.SetBinding(ItemsView.ItemsSourceProperty, new Binding(nameof(Picker.ItemsSource), source: parent));

            static Binding CloneBinding(Binding src) => new Binding(src.Path)
            {
                Mode = BindingMode.OneWay,
                Converter = src.Converter,
                ConverterParameter = src.ConverterParameter,
                StringFormat = src.StringFormat,
                TargetNullValue = src.TargetNullValue,
                FallbackValue = src.FallbackValue
            };

            list.ItemTemplate = new DataTemplate(() =>
            {
                var lbl = new Label
                {
                    Padding = new Thickness(12, 10),
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = 25
                };

                if (parent.ItemDisplayBinding is Binding b)
                    lbl.SetBinding(Label.TextProperty, CloneBinding(b)); // НОВЫЙ экземпляр
                else
                    lbl.SetBinding(Label.TextProperty, ".");

                return lbl;
            });

            list.SelectionChanged += async (s, e) =>
            {
                var selected = e.CurrentSelection?.FirstOrDefault();
                if (selected != null)
                {
                    parent.CommitSelection(selected);

                    // Даем шансов синхронизироваться (обычно Task.Yield достаточно)
                    await Task.Yield();

                    await CloseAsync();
                }
            };

            // Кнопка "Отмена" снизу
            var cancelButton = new Button
            {
                Text = parent.CancelButtonText,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(0, 8, 0, 0),
                WidthRequest = 150
            };
            cancelButton.Clicked += (s, e) => CloseAsync();

            Content = new VerticalStackLayout
            {
                Padding = 12,
                Spacing = 8,
                Children =
                {
                    header,
                    new BoxView { HeightRequest = 1, Opacity = 0.1 },
                    list,
                    cancelButton
                }
            };
        }
    }

}







//list.ItemTemplate = new DataTemplate(() =>
//{
//    var lbl = new Label
//    {
//        Padding = new Thickness(12, 10),
//        VerticalOptions = LayoutOptions.Center
//    };

//    if (parent.ItemDisplayBinding != null)
//        lbl.SetBinding(Label.TextProperty, parent.ItemDisplayBinding);
//    else
//        lbl.SetBinding(Label.TextProperty, ".");

//    return lbl;
//});

// Выбор элемента → установить в Picker и закрыть попап
//list.SelectionChanged += (s, e) =>
//{
//    var selected = e.CurrentSelection?.FirstOrDefault();
//    if (selected != null)
//    {
//        parent.CommitSelection(selected);
//        CloseAsync();
//    }
//};

//list.SelectionChanged += async (s, e) =>
//{
//    var selected = e.CurrentSelection?.FirstOrDefault();
//    if (selected != null)
//    {
//        parent.CommitSelection(selected); // сначала обновляем индекс/текст
//        await CloseAsync();               // затем закрываем попап
//    }
//};
// Шаблон элемента: используем ItemDisplayBinding из Picker, иначе "."
//static Binding CloneBinding(Binding src) => new Binding
//{
//    Path = src.Path,
//    Mode = BindingMode.OneWay,
//    Converter = src.Converter,
//    ConverterParameter = src.ConverterParameter,
//    StringFormat = src.StringFormat,
//    Source = src.Source,                
//    TargetNullValue = src.TargetNullValue,
//    FallbackValue = src.FallbackValue
//};