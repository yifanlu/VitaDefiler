using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using VitaDefiler;
using VitaDefiler.Modules;
using VitaDefiler.PSM;


namespace AndroidDefiler
{
	public class ControlWriter : TextWriter
	{
		private TextView textView;
		private Activity activity;
		public ControlWriter(Activity activity, TextView textbox)
		{
			this.textView = textbox;
			this.activity = activity;
		}

		public override void Write(char value)
		{
			activity.RunOnUiThread (() => textView.Append(value+""));
		}

		public override void Write(string value)
		{
			activity.RunOnUiThread (() => textView.Append(value));
		}

		public override System.Text.Encoding Encoding
		{
			get { return System.Text.Encoding.ASCII; }
		}
	}
	
	[Activity (Label = "AndroidDefiler", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory), typeof(FileIO), typeof(Scripting)};

		public static bool exitAfterInstall = false;

		private const int PICKFILE_RESULT_CODE = 1;

		Button button;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			button = FindViewById<Button> (Resource.Id.myButton);
			TextView textView = FindViewById<TextView> (Resource.Id.myTextView);
			textView.MovementMethod = new Android.Text.Method.ScrollingMovementMethod();
			ControlWriter cw = new ControlWriter(this,textView);
			Console.SetError(cw);
			Console.SetOut(cw);

			button.Click += button_click;



		}
		void button_click(object sender, EventArgs e){
			Intent intent = new Intent(Intent.ActionGetContent);
			intent.SetType ("file/*");
			StartActivityForResult(intent,PICKFILE_RESULT_CODE);
		}
			

		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data){
			switch(requestCode){
			case PICKFILE_RESULT_CODE:
				if(resultCode==Result.Ok){
					String FilePath = data.Data.Path;

					button.Text = "Launching "+FilePath+" !";
					button.Enabled = false;

					string sdcard = Android.OS.Environment.ExternalStorageDirectory.Path;
					var scriptFile = Path.Combine(sdcard,"uvloader.vds");
					var elfFile = FilePath;//Path.Combine(sdcard,"smsppsp.velf");

					string[] args = {scriptFile,elfFile};

					ThreadPool.QueueUserWorkItem (o => VitaDefiler.Program.Main(args));
				}
				break;

			}
		}
			
	}
}


