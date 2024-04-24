using Plugin.Maui.Audio;
using Plugin.LocalNotification;
using MQTTnet.Client;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Server;
using System.Text;
using MQTTnet.Protocol;
using System.Diagnostics;
using System.Globalization;

namespace SoundAlert
{
    public partial class MainPage : ContentPage
    {
        private readonly IAudioManager _audioManager;
        private IAudioPlayer _audioPlayer;
        private IMqttClient _mqttClient;
        private MqttClientConnectResult _connectResult;
        private Animation _pulsingAnimation;
        private bool _tratandoMensagem;
        private double _filtro = 1940;
        private static bool _isAlarmActive = false;
        private static Stopwatch _alarmTimer = new Stopwatch();

        public MainPage(IAudioManager audioManager)
        {
            InitializeComponent();
            _audioManager = audioManager;

            // Add the pulsing animation
            var scaleAnimation = new Animation(v => PulsingIcon.Scale = v, 1, 1.5);
            var fadeAnimation = new Animation(v => PulsingIcon.Opacity = v, 1, 0);

            _pulsingAnimation = new Animation();
            _pulsingAnimation.Add(0, 0.5, scaleAnimation);
            _pulsingAnimation.Add(0.5, 1, fadeAnimation);

        }

        private async Task ConexaoBroker(string id)
        {
            string broker = "broker.emqx.io";
            int port = 1883;
            string clientId = Guid.NewGuid().ToString();
            string topic = "ufabc/" + id;
            string username = "emqx";
            string password = "public";

            // Create a MQTT client factory
            var factory = new MqttFactory();

            // Create a MQTT client instance
            _mqttClient = factory.CreateMqttClient();

            // Create MQTT client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(broker, port) // MQTT broker address and port
                .WithCredentials(username, password) // Set username and password
                .WithClientId(clientId)
                .WithCleanSession()
                .Build();
            try
            {
                _mqttClient.ConnectedAsync += async e =>
                {
                    Console.WriteLine("Connected to MQTT broker.");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LabelDescricao.Text = "Conectado ao Broker MQTT.";
                    });
                };

                _mqttClient.DisconnectedAsync += async e =>
                {
                    Console.WriteLine("Disconnected from MQTT broker.");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    try
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            LabelDescricao.Text = "Dispositivo desconectado.";
                            AbsoluteLayoutConectado.IsVisible = false;
                        });

                        
                    }
                    catch
                    {
                        Console.WriteLine("Reconnect failed.");
                    }
                };


                _connectResult = await _mqttClient.ConnectAsync(options);

                if(_connectResult.ResultCode == MqttClientConnectResultCode.Success)
                {
                    var subscribe = await _mqttClient.SubscribeAsync(topic);

                    // Callback function when a message is received
                    _mqttClient.ApplicationMessageReceivedAsync += async e =>
                    {
                        if (!_tratandoMensagem)
                        {
                            _tratandoMensagem = true;

                            string message = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                            await TrataMensagem(message);

                            Console.WriteLine($"Received message: {message}");

                            _tratandoMensagem = false;
                        }
                    };

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LabelDescricao.Text = "Dispositivo conectado com suceso.";
                        AbsoluteLayoutConectado.IsVisible = true;
                    });
                }
                else
                {
                    Console.WriteLine($"Failed to connect to MQTT broker: {_connectResult.ResultCode}");
                }

                //if (_connectResult.ResultCode == MqttClientConnectResultCode.Success)
                //{
                //    Console.WriteLine("Connected to MQTT broker successfully.");
                  
                //    // Subscribe to a topic
                //    await _mqttClient.SubscribeAsync(topic);

                //    // Callback function when a message is received
                //    _mqttClient.ApplicationMessageReceivedAsync += async e =>
                //    {
                //        if (!_tratandoMensagem)
                //        {
                //            _tratandoMensagem = true;

                //            string message = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                //            await TrataMensagem(message);

                //            Console.WriteLine($"Received message: {message}");

                //            _tratandoMensagem = false;
                //        }
                //    };
                //}
                //else
                //{
                //    Console.WriteLine($"Failed to connect to MQTT broker: {_connectResult.ResultCode}");
                //}

                //while (true)
                //{
                //    await Task.Delay(1000);  // Adjust the delay as needed
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task TrataMensagem(string value)
        {
            try
            {
                if (_alarmTimer.ElapsedMilliseconds > 15000)
                {
                    _isAlarmActive = false;
                    _alarmTimer.Stop();
                    _alarmTimer.Reset();
                    Console.WriteLine("Alarme acionado a mais de 15 segundos");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LabelDescricao.Text = "Dispositivo conectado com suceso.";
                        _pulsingAnimation.Pause();
                        AbsoluteLayoutIcone.IsVisible = false;
                        AbsoluteLayoutConectado.IsVisible = true;
                        Parar.IsVisible = false;
                    });
                }

                if (!string.IsNullOrEmpty(value))
                {
                    var faixa = double.Parse(value, CultureInfo.InvariantCulture);

                    if ((faixa > _filtro + 30 || faixa < _filtro - 30) && !_alarmTimer.IsRunning)
                    {
                        AcionarAlarme();

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                LabelDescricao.Text = "ATENÇÃO: ruído detectado.";
                                AbsoluteLayoutConectado.IsVisible = false;
                                AbsoluteLayoutIcone.IsVisible = true;
                                Parar.IsVisible = true;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        });

                        _isAlarmActive = true;
                        _alarmTimer.Start();

                        Console.WriteLine("ALARME ACIONADO.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid message format.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }

        private async void AcionarAlarme()
        {
            try
            {
                var request = new NotificationRequest
                {
                    NotificationId = 1337,
                    Title = "ALERTA!",
                    Subtitle = "SoundAlert",
                    Description = "Identificamos um sinal sonoro em seu dispositivo.",
                    BadgeNumber = 42,
                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = DateTime.Now.AddSeconds(1)
                    }
                };

                if(await LocalNotificationCenter.Current.Show(request))
                {
                    Console.WriteLine("Notification sent.");
                }

                _audioPlayer = _audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("alarm.wav"));
                _audioPlayer.Play();



                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PulsingIcon.BackgroundColor = Color.FromHex("#FF0000");
                    _pulsingAnimation.Commit(this, "PulseAnimation", length: 2000, repeat: () => true);
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }
        private async void OnCounterClicked(object sender, EventArgs e)
        {
            try
            {
                string retorno = await DisplayPromptAsync("Insira o ID do seu dispositivo", "");
                if (!string.IsNullOrEmpty(retorno))
                    await Task.Run(async () => await ConexaoBroker(retorno));
            }
            catch (Exception ex)
            {

            }

        }

        private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.Stop();
            }

        }

        private async void CounterBtn_Clicked(object sender, EventArgs e)
        {
            try
            {
                string retorno = await DisplayPromptAsync("Insira o valor limite de tolerância:", "Recomendado entre 1000 e 1200");
                _filtro = Convert.ToDouble(retorno);
            }
            catch (Exception ex)
            {

            }

        }
    }

}
