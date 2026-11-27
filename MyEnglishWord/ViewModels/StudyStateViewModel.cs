using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using Microsoft.UI;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Models;

namespace MyEnglishWord.ViewModels;

public partial class StudyStateViewModel : ObservableRecipient
{
    public record class PieChartData(List<WordCountData> Data, int TotalDays);

    public record class ActiveChartData(List<WordCountData> FullList, List<WordCountData> PrefixSum);

    [ObservableProperty]
    public partial List<ISeries>? Active_Series
    {
        get; set;
    }

    [ObservableProperty]
    public partial List<ICartesianAxis>? Active_XAxes
    {
        get; set;
    }

    [ObservableProperty]
    public partial List<ICartesianAxis>? Active_YAxes
    {
        get; set;
    }

    [ObservableProperty]
    public partial int Active_ShowWayIndex
    {
        get; set;
    } = 0;

    [ObservableProperty]
    public partial List<ISeries>? PieChartSeries
    {
        get; set;
    }

    [ObservableProperty]
    public partial ObservableCollection<ExpressPictState> ExpressPictStates
    {
        get; set;
    } = new();

    public ExpressPictState ZeroExpressPictState
    {
        get;
    } = new(0, ExpressPictOp.Equal, Colors.Green, (int x) => x == 0);

    public ExpressPictState NotZeroExpressPictState
    {
        get;
    } = new(0, ExpressPictOp.GreaterThan, Colors.Yellow, (int x) => x > 0);


    public PieChartData? CurPieChartData
    {
        get;
        set;
    }

    public ActiveChartData? CurAvtiveChartData
    {
        get;
        set;
    }

    public StudyStateViewModel()
    {
        ExpressPictStates.Add(ZeroExpressPictState);
        ExpressPictStates.Add(NotZeroExpressPictState);
    }
}
