using System.Collections.Specialized;
using System.Windows.Controls;

namespace MucLucHoSo.App.Views;

public partial class Step4GenerateView : UserControl
{
    public Step4GenerateView()
    {
        InitializeComponent();
        // Nhật ký realtime: mỗi khi có dòng mới, tự cuộn xuống dòng cuối.
        ((INotifyCollectionChanged)LogList.Items).CollectionChanged += OnLogChanged;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
