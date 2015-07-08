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

		StartCoroutine(StartListenerDelayed());
	}

	private IEnumerator StartListenerDelayed()
	{
		yield return new WaitForSeconds(4.0f); // This needs to happen to prevent the "VM not suspended" error from happening.

		// This can be called right away because AcceptSocket will wait until VitaDefiler
		// connects to it, which is after privileges have been escalated.
		VitaDefilerClient.CommandListener.StartListener();
	}

	public void Log(string logString, string stackTrace, LogType type)
	{
		LogLine (logString);
	}
	
	public static void LogLine (string line)
	{
		Debug.Log(line);

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