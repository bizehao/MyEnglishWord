using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Helpers;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using MyEnglishWord.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using WinRT;
using static MyEnglishWord.ViewModels.StudyStateViewModel;

namespace MyEnglishWord.Views;

public sealed partial class StudyStatePage : Page
{

    public StudyStateViewModel ViewModel
    {
        get;
    }

    private readonly List<(ExpressPictState Item, PropertyChangedEventHandler Handler)> _pieStatusHandlers = new();

    public StudyStatePage()
    {
        ViewModel = App.GetService<StudyStateViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {

    }

    private async Task HandleActiveChart()
    {
        var sdate = Active_StartDatePicker.SelectedDate;
        var edate = Active_EndDatePicker.SelectedDate;

        if (sdate is null || edate is null || sdate > edate)
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "日期选择错误",
                Content = "日期不能为空或者开始日期不能大于结束日期，请重新选择。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }


        // 构造测试数据：日期 + 数量
        //var dataPoints = new List<(DateTime Date, int Count)>();

        //var startDate = sdate.Value.Date;
        //var endDate = edate.Value.Date;

        //var startDate = new DateTime(2020, 8, 1);
        //var endDate = new DateTime(2026, 6, 1);

        //var days = (endDate - startDate).Days;

        //if (ViewModel.ShowWayIndex == 0)
        //{
        //    for (var i = 0; i < days; i++)
        //    {
        //        dataPoints.Add((startDate.AddDays(i), Random.Shared.Next(0, 100)));
        //    }
        //}
        //else if (ViewModel.ShowWayIndex == 1)
        //{
        //    var startMonth = new DateTime(startDate.Year, startDate.Month, 1);
        //    var endMonth = new DateTime(endDate.Year, endDate.Month, 1);
        //    var months = ((endMonth.Year - startMonth.Year) * 12) + (endMonth.Month - startMonth.Month);
        //    for (var i = 0; i <= months; i++)
        //    {
        //        var monthDate = startMonth.AddMonths(i);
        //        dataPoints.Add((monthDate, Random.Shared.Next(0, 100)));
        //    }
        //}
        //else if (ViewModel.ShowWayIndex == 2)
        //{
        //    for (var year = startDate.Year; year <= endDate.Year; year++)
        //    {
        //        var yearDate = new DateTime(year, 1, 1);
        //        dataPoints.Add((yearDate, Random.Shared.Next(0, 100)));
        //    }
        //}

        var startDate = sdate.Value.Date;
        var endDate = edate.Value.Date;

        Active_MyChart.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        Active_ChartTitle.Text = "活动图:" + $"({startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})";

        WordCountDataGroupType type = ViewModel.Active_ShowWayIndex switch
        {
            0 => WordCountDataGroupType.Day, // 日
            1 => WordCountDataGroupType.Month, // 月
            2 => WordCountDataGroupType.Year, // 年
            _ => throw new NotImplementedException(),
        };

        var dataPoints = await App.GetService<MyWordRecordContext>().GetWordCount(startDate, endDate, type);

        ViewModel.CurAvtiveChartData = await Task.Run(() =>
        {
            Func<DateTime, int> makeKey = type switch
            {
                WordCountDataGroupType.Year => (DateTime e) => e.Year,
                WordCountDataGroupType.Month => (DateTime e) => e.Year * 100 + e.Month,
                WordCountDataGroupType.Day => (DateTime e) => e.Year * 10000 + e.Month * 100 + e.Day,
                _ => throw new NotImplementedException()
            };

            Func<DateTime, DateTime> makeTime = type switch
            {
                WordCountDataGroupType.Year => (DateTime e) => e.AddYears(1),
                WordCountDataGroupType.Month => (DateTime e) => e.AddMonths(1),
                WordCountDataGroupType.Day => (DateTime e) => e.AddDays(1),
                _ => throw new NotImplementedException()
            };

            var fullList = new List<WordCountData>();
            // 构建字典：key 是日期（只保留日期，消除时分秒）

            var existDict = dataPoints
            .ToDictionary(item => makeKey(item.Time), item => item.Count);

            var currentDay = startDate;
            // 循环补齐每一天
            while (currentDay <= endDate)
            {
                existDict.TryGetValue(makeKey(currentDay), out var cnt);
                fullList.Add(new WordCountData(currentDay, cnt));
                currentDay = makeTime(currentDay);
            }

            var sum = 0;
            var prefixSum = fullList.Select(x => new WordCountData(x.Time, sum += x.Count)).ToList();

            return new ActiveChartData(fullList, prefixSum);
        });
    }

    private void ShowActiveChart(int type)
    {
        if (ViewModel.CurAvtiveChartData is null)
        {
            return;
        }
        if (type == 0) //每日单词记忆图
        {
            // 柱状图，X 用 DateTime（LiveCharts 内部把日期转成 double 时间戳）
            ViewModel.Active_Series =
            [
                new ColumnSeries<WordCountData>
            {
                Values = ViewModel.CurAvtiveChartData.FullList,
                // 映射：X取日期，Y取数量
                Mapping = (item, point) =>
                {
                    return new Coordinate(point, item.Count);
                },
                Fill = new SolidColorPaint(SKColors.SteelBlue),
                Name = "单词量",
            }
            ];
        }
        else if (type == 1) //单词总量增长图
        {
            // 柱状图，X 用 DateTime（LiveCharts 内部把日期转成 double 时间戳）
            ViewModel.Active_Series =
            [
            new LineSeries<WordCountData>
            {
                Values = ViewModel.CurAvtiveChartData.PrefixSum,
                // 映射：X取日期，Y取数量
                Mapping = (item, point) =>
                {
                    return new Coordinate(point, item.Count);
                },
                Stroke = new SolidColorPaint(SKColors.IndianRed),
                Fill = null,
                LineSmoothness = 0,
                Name = "单词总量",
            }
            ];
        }
        else
        {
            throw new NotImplementedException();
        }

        Func<DateTime, string> formatFun = ViewModel.Active_ShowWayIndex switch
        {
            0 => dt => dt.ToString("yy-MM-dd"), // 日
            1 => dt => dt.ToString("yy-MM"), // 月
            2 => dt => dt.ToString("yy"), // 年
            _ => throw new NotImplementedException(),
        };

        // X轴：日期格式化
        ViewModel.Active_XAxes =
        [
            new Axis
            {
                Labeler = value =>
                {
                    var v = (int)Math.Round(value);
                    if(v < 0 || v >= ViewModel.CurAvtiveChartData.FullList.Count)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return formatFun(ViewModel.CurAvtiveChartData.FullList[v].Time); // 使用选定的格式化函数
                    }
                },
                UnitWidth = 1,
                MinStep = 1,
                MinSeparators = 5,
                Name = "时间"
                //MinLimit = startDate.Ticks,
                //MaxLimit = endDate.Ticks
            }
        ];

        ViewModel.Active_YAxes =
        [
            new Axis
            {
                Name = "数量",
                Labeler = v => v.ToString("N0"),
                MinLimit = 0
            }
        ];
    }

    private async void Active_ChartButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await HandleActiveChart();
        ShowActiveChart(RadioButtons_ActiveChartType.SelectedIndex);
    }

    private async Task HandlePieChart()
    {
        var sdate = Increase_StartDatePicker.SelectedDate;
        var edate = Increase_EndDatePicker.SelectedDate;

        if (sdate is null || edate is null || sdate > edate)
        {
            var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                Title = "日期选择错误",
                Content = "日期不能为空或者开始日期不能大于结束日期，请重新选择。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        var startDate = sdate.Value.Date;
        var endDate = edate.Value.Date;
        endDate = endDate.AddHours(23).AddMinutes(59).AddSeconds(59);

        PieChart.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        Increase_ChartTitle.Text = "学习图:" + $"({startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd})";

        var data = await App.GetService<MyWordRecordContext>().GetWordCount(startDate, endDate, WordCountDataGroupType.Day);
        ViewModel.CurPieChartData = new(data, (endDate - startDate).Days + 1);

        await UodatePieChart();
    }

    private async void Increase_ChartButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await HandlePieChart();
    }

    private async Task UodatePieChart()
    {
        if (ViewModel.CurPieChartData is null)
        {
            return;
        }
        //清除之前的颜色观测
        foreach (var pair in _pieStatusHandlers)
        {
            pair.Item.PropertyChanged -= pair.Handler;
        }
        _pieStatusHandlers.Clear();

        var epss = ViewModel.ExpressPictStates.ToList();
        epss.Remove(ViewModel.ZeroExpressPictState);

        var result = await Task.Run(() =>
        {
            var map = new Dictionary<ExpressPictState, int>();
            foreach (var it in ViewModel.CurPieChartData.Data)
            {
                foreach (var exss in epss)
                {
                    if (exss.Express(it.Count))
                    {
                        ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(map, exss, out _);
                        count++;
                        break;
                    }
                }
            }
            return map;
        });

        var total = ViewModel.CurPieChartData.TotalDays;
        var validCount = ViewModel.CurPieChartData.Data.Count();

        var ts = new List<ISeries>();

        result.Add(ViewModel.ZeroExpressPictState, total - validCount);

        foreach (var it in result)
        {
            var exStatus = it.Key;
            var pss = new PieSeries<double>
            {
                Values = new double[] { it.Value },
                Name = $"学习{it.Key}天",
                Fill = new SolidColorPaint(exStatus.Color.ToSKColor())
            };
            ts.Add(pss);

            PropertyChangedEventHandler handler = (object? sender, PropertyChangedEventArgs e) =>
            {
                if (sender == exStatus && e.PropertyName == nameof(exStatus.Color))
                {
                    pss.Fill = new SolidColorPaint(exStatus.Color.ToSKColor());
                }
            };

            exStatus.PropertyChanged += handler;

            _pieStatusHandlers.Add((exStatus, handler));
        }

        ViewModel.PieChartSeries = ts;
    }

    private void ExpressPictStateListView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {

        IncrementNumberRounder rounder = new IncrementNumberRounder
        {
            Increment = 1,
            RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp
        };

        DecimalFormatter formatter = new DecimalFormatter
        {
            IntegerDigits = 1,
            FractionDigits = 0,
            NumberRounder = rounder
        };
        MyNumberBox.NumberFormatter = formatter;

    }

    private async void ExpressAdd_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        //&lt;<   &gt;>
        var v = int.Parse(MyNumberBox.Text);
        var index = ExpressOp.SelectedIndex;

        ExpressPictOp op = index switch
        {
            0 => ExpressPictOp.Equal,
            1 => ExpressPictOp.GreaterThan,
            _ => throw new NotImplementedException(),
        };

        if (ViewModel.ExpressPictStates.Any(e => e.Value == v && e.Op == op))
        {
            // 值已存在，不添加重复项
            TestButton1TeachingTip.IsOpen = true;
            return;
        }


        if (index == 0)
        {
            ViewModel.ExpressPictStates.Add(new ExpressPictState(v, ExpressPictOp.Equal, ExpressColorPicker.SelectedColor, (int x) => x == v));
        }
        else if (index == 1)
        {
            ViewModel.ExpressPictStates.Add(new ExpressPictState(v, ExpressPictOp.GreaterThan, ExpressColorPicker.SelectedColor, (int x) => x > v));
        }

        ViewModel.ExpressPictStates.Sort((l, r) => {
            if (l.Value == r.Value)
            {
                return r.Op - l.Op;
            }
            return r.Value - l.Value;
        });

        AddExpress.Flyout.Hide();

        await UodatePieChart();
    }

    private void StackPanel_ContextRequested(UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs args)
    {
        // 当前行的数据模型
        if (sender is StackPanel sp)
        {
            var itemModel = sp.DataContext as ExpressPictState;
            if (itemModel is not null)
            {
                if (itemModel != ViewModel.ZeroExpressPictState && itemModel != ViewModel.NotZeroExpressPictState)
                {
                    var flyout = new MenuFlyout();
                    System.Windows.Input.ICommand cmd = new RelayCommand<ExpressPictState>((e) => {
                        ViewModel.ExpressPictStates.Remove(e!);
                    });
                    var deleteItem = new MenuFlyoutItem
                    {
                        Text = "删除",
                        Command = cmd,
                        CommandParameter = itemModel
                    };
                    flyout.Items.Add(deleteItem);
                    // 显示在当前Item
                    if (args.TryGetPosition(sender, out var point))
                    {
                        flyout.ShowAt(sender, point);
                    }
                }
            }
        }
    }

    private async void Active_loaded(object sender, RoutedEventArgs e)
    {
        var sdate = DateTime.Now.Date.AddDays(-6);
        var edate = DateTime.Now;
        Active_StartDatePicker.SelectedDate = sdate;
        Active_EndDatePicker.SelectedDate = edate;

        await HandleActiveChart();
        ShowActiveChart(RadioButtons_ActiveChartType.SelectedIndex);
    }

    private async void Increase_loaded(object sender, RoutedEventArgs e)
    {
        var sdate = DateTime.Now.Date.AddDays(-6);
        var edate = DateTime.Now;
        Increase_StartDatePicker.SelectedDate = sdate;
        Increase_EndDatePicker.SelectedDate = edate;

        await HandlePieChart();
    }

    private void RadioButton_WordPerDay_Checked(object sender, RoutedEventArgs e)
    {
        ShowActiveChart(0);
    }

    private void RadioButton_WordTotal_Checked(object sender, RoutedEventArgs e)
    {
        ShowActiveChart(1);
    }
}
