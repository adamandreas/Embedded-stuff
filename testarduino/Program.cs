using System;
using System.IO.Ports;

class Program
{
    static void Main()
    {
        // List available serial ports
        Console.WriteLine("Available serial ports:");
        string[] ports = SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            Console.WriteLine("No serial ports found.");
            return;
        }
        
        foreach (string portName in ports)
        {
            Console.WriteLine($"  - {portName}");
        }
        Console.WriteLine();

        string portNameToUse = "/dev/cu.usbserial-0001";
        
        // Check if the specified port exists
        if (!Array.Exists(ports, p => p == portNameToUse))
        {
            Console.WriteLine($"Warning: Port '{portNameToUse}' not found in available ports.");
            if (ports.Length > 0)
            {
                Console.WriteLine($"Using first available port: {ports[0]}");
                portNameToUse = ports[0];
            }
            else
            {
                Console.WriteLine("No ports available. Exiting.");
                return;
            }
        }

        SerialPort port = null;
        try
        {
            port = new SerialPort(portNameToUse, 9600);
            port.Open();
            Console.WriteLine($"Successfully opened port: {portNameToUse}");
            Console.WriteLine("Reading data (press Ctrl+C to exit)...\n");

            while (true)
            {
                string line = port.ReadLine();
                Console.WriteLine(line);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied to port '{portNameToUse}':");
            Console.WriteLine($"  {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner exception: {ex.InnerException.Message}");
            }
            Console.WriteLine("\nPossible solutions:");
            Console.WriteLine("  1. Make sure no other application is using the port");
            Console.WriteLine("  2. Check if you have permission to access the port");
            Console.WriteLine("  3. Try running with sudo (not recommended for development)");
            Console.WriteLine("  4. Check if the port exists: ls -l /dev/cu.*");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
        }
        finally
        {
            if (port != null && port.IsOpen)
            {
                port.Close();
                Console.WriteLine("\nPort closed.");
            }
        }
    }
}