using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SpaceMonitor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> messages = new();

    [ObservableProperty]
    private string status = "Not connected";

    private SerialPort? serialPort;

    public MainWindowViewModel()
    {
    }

    [RelayCommand]
    public void startReadingButton()
    {
        _ = Task.Run(StartReading);
    }

    private void StartReading()
    {
        try
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length == 0)
            {
                Status = "No serial ports found";
                return;
            }

            string portName = "/dev/cu.usbserial-0001";
            if (!Array.Exists(ports, p => p == portName))
            {
                portName = ports[0];
            }

            serialPort = new SerialPort(portName, 9600);
            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.Open();
            
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
        if (serialPort == null || !serialPort.IsOpen) return;

        try
        {
            string line = serialPort.ReadLine();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Messages.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
                if (Messages.Count > 100) // Keep last 100 messages
                {
                    Messages.RemoveAt(0);
                }
            });
        }
        catch { }
    }

}