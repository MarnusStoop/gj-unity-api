﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GJAPI
{
	public class Manager : MonoBehaviour
	{
		#region Singleton
		protected static Manager instance;
		public static Manager Instance
		{
			get
			{
				if (instance == null) 
				{
					instance = FindObjectOfType<Manager>();
					
					if (instance == null)
					{
						Debug.LogError("An instance of " + typeof(Manager) + " is needed in the scene, but there is none.");
					}
				}
				
				return instance;
			}
		}
		#endregion Singleton

		#region Fields & Properties
		public int GameID { get; private set; }
		public string PrivateKey { get; private set; }

		public float Timeout { get; set; }
		public bool AutoPing { get; private set; }

		public Objects.User CurrentUser { get; set; }

#if UNITY_EDITOR
		bool DebugAutoConnect { get; set; }
		string DebugUser { get; set; }
		string DebugToken { get; set; }
#endif
		#endregion Fields & Properties

		#region Init
		void Awake()
		{
			if (Persist())
			{
				Configure();
				AutoConnectWebPlayer();
			}
		}

		bool Persist()
		{
			if (instance == null)
			{
				instance = this;
			}
			else if (instance != this)
			{
				Destroy(this.gameObject);
				return false;
			}

			DontDestroyOnLoad(this.gameObject);
			return true;
		}

		void Configure()
		{
			var settings = Resources.Load(Constants.SETTINGS_ASSET_NAME) as Settings;
			GameID = settings.gameId;
			PrivateKey = settings.privateKey;
			Timeout = settings.timeout;
			AutoPing = settings.autoPing;

			if (GameID == 0)
			{
				Debug.LogWarning("Missing Game ID.");
			}
			if (PrivateKey == string.Empty)
			{
				Debug.LogWarning("Missing Private Key.");
			}

#if UNITY_EDITOR
			DebugAutoConnect = settings.autoConnect;
			DebugUser = settings.user;
			DebugToken = settings.token;
#endif
		}
		#endregion Init

		#region Requests
		public IEnumerator GetRequest(string url, Core.ResponseFormat format, Action<Core.Response> callback)
		{
			float timeout = Time.time + Timeout;

			var www = new WWW(url);
			while (!www.isDone)
			{
				if (Time.time > timeout)
				{
					callback(new Core.Response("Timeout"));
					yield break;
				}
				yield return new WaitForEndOfFrame();
			}

			callback(new Core.Response(www, format));
		}

		public IEnumerator PostRequest(string url, Dictionary<string, string> payload, Core.ResponseFormat format, Action<Core.Response> callback)
		{
			var form = new WWWForm();
			foreach (KeyValuePair<string,string> field in payload)
			{
				form.AddField(field.Key, field.Value);
			}

			float timeout = Time.time + Timeout;

			var www = new WWW (url, form);
			while (!www.isDone)
			{
				if (Time.time > timeout)
				{
					callback(new Core.Response("Timeout"));
					yield break;
				}
				yield return new WaitForEndOfFrame();
			}

			callback(new Core.Response(www, format));
		}
		#endregion Requests

		#region Actions
		void AutoConnectWebPlayer()
		{
#if UNITY_WEBPLAYER || UNITY_WEBGL
	#if UNITY_EDITOR
			if (DebugAutoConnect)
			{
				if (DebugUser != string.Empty && DebugToken != string.Empty)
				{
					var user = new Objects.User(DebugUser, DebugToken);
					user.SignIn((bool success) => { Debug.Log(string.Format("AutoConnect: " + success)); });
				}
				else
				{
					Debug.LogWarning("Cannot simulate WebPlayer AutoConnect. Missing user and/or token in debug settings.");
				}
			}
	#else
			var uri = new System.Uri(Application.absoluteURL);
			if (uri.Host.EndsWith("gamejolt.net") || uri.Host.EndsWith("gamejolt.com"))
			{
				Application.ExternalCall("GJAPI_AuthUser", this.gameObject.name, "OnAutoConnectWebPlayer");
			}
			else
			{
				Debug.Log("Cannot AutoConnect, the game is not hosted on GameJolt.");
			}
	#endif
#endif
		}

		public void OnAutoConnectWebPlayer(string response)
		{
			if (response != string.Empty)
			{
				var credentials = response.Split(':');
				if (credentials.Length == 2)
				{
					var user = new Objects.User(credentials[0], credentials[1]);
					user.SignIn();
					// TODO: Prompt "Welcome Back <username>!"
				}
				else
				{
					Debug.Log("Cannot AutoConnect.");
				}
			}
			else
			{
				// This is a Guest.
				// TODO: Prompt "Hello Guest!" and encourage to signup/signin?
			}
		}

		public void StartAutoPing()
		{
			if (!AutoPing)
			{
				return;
			}

			Sessions.Open((bool success) => {
				// What should we do if it fails? Retry later?
				// Without smart handling, it will probably just fail again...
				if (success)
				{
					Invoke("Ping", 30f);
				}
			});
		}

		void Ping()
		{
			Sessions.Ping(SessionStatus.Active, (bool success) => {
				// Sessions are automatically closed after 120 seconds
				// which will happen if the application has been in the background for too long.
				// It would be nice to Ping an Idle state when the app is in the background,
				// but because Unity apps don't run in the background by default, this is doomed to failure.
				// Let it error out and reconnect.
				if (!success)
				{
					Invoke("StartAutoPing", 1f); // Try reconnecting.
					return;
				}
				else
				{
					Invoke("Ping", 30f); // Ping again.
				}
			});
		}

		public void StopAutoPing()
		{
			if (AutoPing)
			{
				CancelInvoke("StartAutoPing");
				CancelInvoke("Ping");
			}
		}
		#endregion Actions
	}
}