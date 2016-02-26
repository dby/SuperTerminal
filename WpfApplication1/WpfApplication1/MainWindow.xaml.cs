using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApplication1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Term.Prompt = "# ";

            Loaded += (s, e) => {
                Term.CommandEntered += (ss, ee) => {

                    string msgmsg = ee.Command.GetDescription("Command is '{0}'", " with args '{0}'", ", '{0}'", ".");
                    Term.InsertNewPrompt();
                    Term.InsertLineBeforePrompt(msgmsg);
                };

                Term.AbortRequested += (ss, ee) => {
                    MessageBox.Show("Abort !");
                };

                Term.RegisteredCommands.Add("hello");
                Term.RegisteredCommands.Add("world");
                Term.RegisteredCommands.Add("helloworld");
                Term.RegisteredCommands.Add("ls");
                Term.RegisteredCommands.Add("cd");
                Term.RegisteredCommands.Add("pwd");

                string msg = "Welcome !\n";
                msg += "Hit tab to complete your current command.\n";
                msg += "Use ctrl+c to raise an AbortRequested event.\n\n";
                msg += "Available (fake) commands are:\n";
                Term.RegisteredCommands.ForEach(cmd => msg += "  - " + cmd + "\n");

                Term.InsertNewPrompt();
                Term.InsertLineBeforePrompt(msg);
            };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int cnt = 0;
            Timer timer = new Timer(10);
            timer.Elapsed += (ss, ee) => {
                cnt++;
                if (cnt == 10000)
                    timer.Stop();

                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => {
                    Term.InsertLineBeforePrompt("Hello world ! Number " + cnt + "\r\n");
                }));
            };
            timer.Start();
        }
    }
}
