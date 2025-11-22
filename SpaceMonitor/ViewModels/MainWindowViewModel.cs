using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Series;

namespace SpaceMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private PlotModel plotModel;

    [ObservableProperty]
    private string status = "Not connected";

    private SerialPort? serialPort;
    private readonly LineSeries dataSeries;
    private readonly List<DataPoint> dataPoints = new();
    private DateTime windowStartTime;
    private const double WindowDurationMinutes = 25.0;
    
    // Data validation ranges (adjust based on your sensor's expected values)
    private const double MinValidValue = double.MinValue; // Set to your minimum expected value
    private const double MaxValidValue = double.MaxValue; // Set to your maximum expected value
    private const int StabilizationDelayMs = 500; // Delay after opening port before reading

    public MainWindowViewModel()
    {
        PlotModel = new PlotModel { Title = "Serial Port Data" };
        
        dataSeries = new OxyPlot.Series.LineSeries
        {
            Title = "Data",
            Color = OxyColors.Blue,
            MarkerType = MarkerType.None
        };
        
        PlotModel.Series.Add(dataSeries);
        
        PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Bottom,
            Title = "Time (minutes)",
            Minimum = 0,
            Maximum = WindowDurationMinutes,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot
        });
        
        PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
        {
            Position = OxyPlot.Axes.AxisPosition.Left,
            Title = "Value",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot
        });

        windowStartTime = DateTime.Now;
        
        _ = Task.Run(async () => await StartReading());
        
        var resetTimer = new System.Timers.Timer(TimeSpan.FromMinutes(WindowDurationMinutes).TotalMilliseconds);
        resetTimer.Elapsed += (s, e) => ResetWindow();
        resetTimer.AutoReset = true;
        resetTimer.Start();
    }

    private async Task StartReading()
    {
        try
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Status = "No serial ports found";
                });
                return;
            }

            string portName = "/dev/cu.usbserial-0001";
            if (!Array.Exists(ports, p => p == portName))
            {
                portName = ports[0];
            }

            serialPort = new SerialPort(portName, 9600)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.Open();
            
            // Flush any stale data from the buffers
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            
            // Wait for device to stabilize after connection
            await Task.Delay(StabilizationDelayMs);
            
            // Flush again after stabilization to clear any initialization data
            serialPort.DiscardInBuffer();
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Status = $"Connected to {portName}";
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Status = $"Error: {ex.Message}";
            });
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (serialPort is not { IsOpen: true }) return;

        try
        {
            // Check if there's data available before reading
            if (serialPort.BytesToRead == 0) return;
            
            var line = serialPort.ReadLine().Trim();
            
            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line)) return;
            
            if (double.TryParse(line, out var value))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AddDataPoint(value);
                });
            }
        }
        catch (TimeoutException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Serial port error: {ex.Message}");
        }
    }

    private void AddDataPoint(double value)
    {
        // Validate data range
        if (value is < MinValidValue or > MaxValidValue)
        {
            // Invalid value, skip it
            return;
        }

        DateTime now = DateTime.Now;
        double minutesFromStart = (now - windowStartTime).TotalMinutes;

        if (minutesFromStart >= WindowDurationMinutes)
        {
            ResetWindow();
            minutesFromStart = 0;
        }

        var point = new DataPoint(minutesFromStart, value);
        dataPoints.Add(point);
        dataSeries.Points.Add(point);

        var cutoffTime = Math.Max(0, minutesFromStart - WindowDurationMinutes);
        var pointsToRemove = dataPoints.Where(p => p.X < cutoffTime).ToList();
        foreach (var pt in pointsToRemove)
        {
            dataPoints.Remove(pt);
            dataSeries.Points.Remove(pt);
        }

        if (dataSeries.Points.Count > 0)
        {
            var yValues = dataSeries.Points.Select(p => p.Y).ToList();
            var minY = yValues.Min();
            var maxY = yValues.Max();
            var range = maxY - minY;
            
            if (range > 0.001)
            {
                var padding = range * 0.1;
                PlotModel.Axes[1].Minimum = minY - padding;
                PlotModel.Axes[1].Maximum = maxY + padding;
            }
            else
            {
                PlotModel.Axes[1].Minimum = minY - 1;
                PlotModel.Axes[1].Maximum = maxY + 1;
            }
        }

        PlotModel.InvalidatePlot(true);
    }

    private void ResetWindow()
    {
        windowStartTime = DateTime.Now;
        dataPoints.Clear();
        dataSeries.Points.Clear();
        PlotModel.InvalidatePlot(true);
    }
}
