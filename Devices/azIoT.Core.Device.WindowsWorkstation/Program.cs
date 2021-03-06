﻿namespace azIoT.Core.Device.ConsoleApp
{
    using common = azIoT.Standard.Common;
    using device_common = azIoT.Standard.Device.Common;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using System;
    using System.Text;
    using azIoT.Standard.Device.Common;
    using System.Threading.Tasks;

    class Program
    {
        static ILogger _logger;
        static IConfiguration _appConfig;
        static device_common.IWorkstation workstation;
        static DeviceClient _deviceClient;

        static async void ReceiveIoTHubMessagesAsync()
        {
            while (true)
            {
                try
                {
                    Message message = await _deviceClient.ReceiveAsync();

                    if (null == message)
                    {
                        continue;
                    }

                    _logger.LogInformation(Encoding.ASCII.GetString(message.GetBytes()));

                    string commandType = null != message.Properties && message.Properties.ContainsKey(common.Constants.CommandType) ? Convert.ToString(message.Properties[common.Constants.CommandType]) : string.Empty;

                    if (commandType.Equals(common.Constants.CommandTypes.Session, StringComparison.InvariantCultureIgnoreCase))
                    {
                        workstation.ExecuteSystemCommand(null);
                    }

                    await _deviceClient.CompleteAsync(message);
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, exception.Message);
                }
                finally
                {

                }
            }
        }

        static void Init_azIoTDevice()
        {
            try
            {
                string iotHubHostName = _appConfig["IOT_HUB_HOSTNAME"];
                string deviceId = _appConfig["DEVICE_ID"];
                string deviceKey = _appConfig["DEVICE_KEY"];

                _deviceClient = DeviceClient.Create(iotHubHostName, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKey));

                _deviceClient.SetMethodHandlerAsync(device_common.Constants.Workstation.Commands.LOCK, onLock, null);

                _deviceClient.SetMethodHandlerAsync(device_common.Constants.Workstation.Commands.LOGOFF,
onLock, null);

                _deviceClient.SetMethodHandlerAsync(device_common.Constants.Workstation.Commands.CAPTURE_SCREEN,
onScreenCapture, null);

                _logger.LogTrace(device_common.Constants.Messages.Device_Client_Initialized);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, exception.Message);
            }
            finally
            {
                //_deviceClient.Dispose();
            }
        }

        static Task<MethodResponse> onLock(MethodRequest methodRequest, object userContext)
        {
            _logger.LogTrace($"Method Invoked - {device_common.Constants.Workstation.Commands.LOCK}");
            workstation.ExecuteSystemCommand(device_common.Constants.Workstation.Commands.LOCK);
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes($"'{device_common.Constants.Workstation.Commands.LOCK}'"), 200));
        }

        static Task<MethodResponse> onScreenCapture(MethodRequest methodReq, object userContext)
        {
            _logger.LogTrace($"Method Invoked - {device_common.Constants.Workstation.Commands.CAPTURE_SCREEN}");
            workstation.ExecuteSystemCommand(device_common.Constants.Workstation.Commands.CAPTURE_SCREEN);
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes($"'{device_common.Constants.Workstation.Commands.CAPTURE_SCREEN}'"), 200));
        }

        static ServiceProvider ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IWorkstation, WindowsWorkstation>();
            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
            serviceCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            loggerFactory.AddNLog();
            NLog.LogManager.LoadConfiguration("nlog.config");

            return serviceProvider;
        }

        static void Main(string[] args)
        {
            try
            {

                ServiceProvider serviceProvider = ConfigureServices(new ServiceCollection());

                _logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Program>();
                _logger.LogTrace(common.Constants.Messages.Logger_Initialized);

                _appConfig = new ConfigurationBuilder()
                    .AddJsonFile("appconfig.json", true, true)
                    .Build();

                Init_azIoTDevice();
                workstation = serviceProvider.GetService<device_common.IWorkstation>();
                workstation.Init(_deviceClient);
                ReceiveIoTHubMessagesAsync();

                _logger.LogTrace(device_common.Constants.Messages.Device_Connected);
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, exception.Message);
            }
            finally
            {
                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
                _deviceClient.SetMethodHandlerAsync("lock", null, null);
                //NLog.LogManager.Shutdown();
            }
        }
    }
}