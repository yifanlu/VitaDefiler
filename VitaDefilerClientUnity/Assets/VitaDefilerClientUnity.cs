using UnityEngine;
using System.Collections;

public class VitaDefilerClientUnity : MonoBehaviour {
	private static readonly int LOG_SIZE = 20;
	private static string log;
	private GUIText logtext;

	public VitaDefilerClientUnity () {
		log = string.Empty;
		for (int i = 0; i < LOG_SIZE; i++)
		{
			log += "\n";
		}
	}

	// Use this for initialization
	void Start () {
		logtext = GameObject.Find ("log_text").guiText;
		VitaDefilerClient.AppMain.Start ();
	}

	void OnEnable () {
		Application.RegisterLogCallbackThreaded (Log);
	}

	void OnDisable () {
		Application.RegisterLogCallbackThreaded (null);
	}

	public void Log(string logString, string stackTrace, LogType type)
	{
		LogLine (logString);
	}
	
	public static void LogLine (string line)
	{
		int lines = log.Length - log.Replace("\n", "").Length;
		if (lines >= LOG_SIZE)
		{
			log = log.Substring(log.IndexOf('\n')+1);
		}
		log += line + "\n";
	}

	[System.Diagnostics.Conditional("UNITY_EDITOR")]
	void FixedUpdate() {
		//print ("logging");
		logtext.text = log;
	}
}