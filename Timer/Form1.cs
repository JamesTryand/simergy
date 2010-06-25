using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Timer
{
	public partial class Form1 : Form
	{
		TimeSpan remaining = new TimeSpan();
		DateTime goal = new DateTime(2006, 5, 15, 21, 0, 0);

		public Form1()
		{
			InitializeComponent();
		}

		private void label1_Click(object sender, EventArgs e)
		{

		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			TimeSpan remaining = goal - DateTime.Now;
			label1.Text = remaining.ToString();
		}
	}
}