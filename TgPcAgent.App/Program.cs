using System.Text;
using TgPcAgent.App.Forms;
using TgPcAgent.App.Services;
using TgPcAgent.App.Tray;
using TgPcAgent.Core.Interop;

namespace TgPcAgent.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        if (SynchronizationContext.Current is null)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }

        using var singleInstanceCoordinator = new SingleInstanceCoordinator();
        if (!singleInstanceCoordinator.TryAcquirePrimaryOwnership())
        {
            var choice = ExistingInstanceDialog.ShowChoice();
            if (choice == ExistingInstanceChoice.Cancel)
            {
                return;
            }

            var command = choice == ExistingInstanceChoice.RestartExisting
                ? InstanceControlCommand.RestartExisting
                : InstanceControlCommand.ShutdownExisting;
            var signalSent = singleInstanceCoordinator.TrySendCommandAsync(command, TimeSpan.FromSeconds(4)).GetAwaiter().GetResult();
            if (!signalSent)
            {
                MessageBox.Show(
                    "Не удалось связаться с уже запущенным экземпляром TgPcAgent.",
                    "TgPcAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (choice == ExistingInstanceChoice.CloseExisting)
            {
                MessageBox.Show(
                    "Команда на закрытие отправлена текущему экземпляру.",
                    "TgPcAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!singleInstanceCoordinator.WaitForPrimaryOwnership(TimeSpan.FromSeconds(10)))
            {
                MessageBox.Show(
                    "Предыдущий экземпляр не закрылся вовремя.",
                    "TgPcAgent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        using var agentApplicationContext = new AgentApplicationContext();
        singleInstanceCoordinator.StartServer(agentApplicationContext.HandleInstanceControlCommandAsync, agentApplicationContext.Logger);
        Application.Run(agentApplicationContext);
    }
}
