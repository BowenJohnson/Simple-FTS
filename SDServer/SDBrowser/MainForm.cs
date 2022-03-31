// Bowen Johnson

using System;
using System.Windows.Forms;

namespace SDBrowser
{
    public partial class MainForm : Form
    {
        private ContentFetcher fetcher;

        public MainForm()
        {
            // default command line values
            string prsIP = "127.0.0.1";
            ushort prsPort = 30000;

            // -prs < PRS IP address>:< PRS port >
            // NOTE: args[0] is the name of the program, first true argument is at args[1]
            //string[] args = Environment.GetCommandLineArgs();


            // instantiate the fetcher and add the support SD and FT protocols
            fetcher = new ContentFetcher();
            fetcher.AddProtocol("SD", new SDProtocolClient(prsIP, prsPort));
            fetcher.AddProtocol("FT", new FTProtocolClient(prsIP, prsPort));

            InitializeComponent();
        }

        private void buttonGo_Click(object sender, EventArgs e)
        {
            // user clicked the Go! button

            try
            {
                // grab the address from the address bar
                string address = textboxAddress.Text;

                // fetch the content
                string content = fetcher.Fetch(address);

                // put the content in the content box
                textboxContent.Text = content;
            }
            catch (Exception ex)
            {
                // show error message
                MessageBox.Show(ex.Message);
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // close the fetcher so it can close it's sessions with the servers
            try
            {
                fetcher.Close();
            }
            catch (Exception ex)
            {
                // show error message
                MessageBox.Show(ex.Message);
            }
        }

        private void textboxContent_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
