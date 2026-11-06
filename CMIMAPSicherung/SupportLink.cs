using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
namespace CMIMAPSicherung
{
    internal static class SupportLink
    {
        public static void Show(IWin32Window owner)
        {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"support-url.txt");
            string value=File.Exists(path)?File.ReadAllText(path).Trim():"";Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="www.paypal.com"||uri.AbsolutePath!="/donate/"||!uri.Query.Contains("hosted_button_id=")) {
                MessageBox.Show(owner,"Die Unterstützung ist freiwillig. Alle Programmfunktionen bleiben unabhängig davon verfügbar.\r\n\r\nDer freigegebene öffentliche PayPal-Link ist in diesem Paket noch nicht hinterlegt. Es werden keine Bank- oder Zahlungszugangsdaten benötigt.","Projekt unterstützen",MessageBoxButtons.OK,MessageBoxIcon.Information);return;
            }
            if(MessageBox.Show(owner,"Die Unterstützung ist freiwillig und schaltet keine Funktionen frei.\r\n\r\nÖffentliche PayPal-Unterstützungsseite im Browser öffnen?","Projekt unterstützen",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});
        }
    }
}
