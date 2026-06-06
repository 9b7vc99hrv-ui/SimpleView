using System;
using System.Windows.Forms;
namespace SimpleView_DepthToPointCloud
{
    class Program
    {
       static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

