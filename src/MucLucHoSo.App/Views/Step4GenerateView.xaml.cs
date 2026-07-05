using System;
using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MucLucHoSo.App.Views;

public partial class Step4GenerateView : UserControl
{
    private bool _scrollQueued;

    public Step4GenerateView()
    {
        InitializeComponent();
        // Nhật ký realtime: khi có dòng mới, cuộn xuống cuối — gộp nhiều lần thêm thành 1 lần cuộn.
        ((INotifyCollectionChanged)LogList.Items).CollectionChanged += OnLogChanged;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _scrollQueued) return;
        _scrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _scrollQueued = false;
            if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[^1]);
        }));
    }
}
