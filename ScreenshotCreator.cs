using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

// ScreenshotCreator by The Topicbird - talk@thetopicbird.com

[ExecuteInEditMode] public class ScreenshotCreator : MonoBehaviour {
	[System.Serializable] public class CameraObject {
		public GameObject cam;
		public bool deleteQuestion = false;
		public enum Hotkey {Hotkey, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z};
		public Hotkey hotkey = Hotkey.Hotkey;
	}

	[HideInInspector] public Color signatureColor = new Color (1f, 0f, 0.5f);

	public List <CameraObject> list = new List<CameraObject>();

	[HideInInspector] public bool foldoutSettings = false;

	// name settings
	[Tooltip("The name of your screenshot or screenshot session. Camera name and current date will be added automatically.")]
	public string customName = "";
	public string customDirectory = "";
	[HideInInspector] public int lastCamID = 0;
	[HideInInspector] public Camera lastCam;
	public bool includeCamName = true;
	public bool includeDate = true;
	public bool includeResolution = true;

	// type settings
	public enum FileType {PNG, JPG};
	public FileType fileType;

	public enum CaptureMethod {CaptureScreenshot, RenderTexture, Cutout};
	public CaptureMethod captureMethod;

	public bool singleCamera = false;
	public float renderSizeMultiplier = 1f;
	public int captureSizeMultiplier = 1;

	public Vector2 cutoutPosition;
	public Vector2 cutoutSize;
	GUIStyle cutoutBoxStyle = null;

	public bool applicationPath = false;

	public void Create(){
		list.Add (new CameraObject());
	}

	public void RequestDelete (int id){
		list [id].deleteQuestion = true;
	}

	public void Delete (int id){
		list.Remove (list [id]);

		if (list.Count == 0) {
			Create ();
		}
	}

	void Awake(){
		if (list.Count == 0) {
			Create ();
			list [0].cam = Camera.main.gameObject;
		}
	}
		
	void LateUpdate(){
		if (Input.anyKeyDown) {
			for (int i = 0; i < list.Count; i++) {
				if ((int)list [i].hotkey == 0) {
					continue;
				}
					
				if (Input.GetKeyDown(list [i].hotkey.ToString ().ToLower())){
					if (list [i] != null) {
						if (captureMethod == CaptureMethod.RenderTexture) {
							Camera attachedCam = list [i].cam.GetComponent<Camera> ();
							if (attachedCam == null) {
								CaptureScreenshots (i, true);
							} else {
								CaptureRenderTexture (attachedCam, i);
							}
						} else if (captureMethod == CaptureMethod.CaptureScreenshot) {
							CaptureScreenshots (i, false);
						} else {
							StartCoroutine(CaptureCutout (i));
						}

						lastCam = list [lastCamID].cam.GetComponent<Camera> ();
					} else {
						Debug.Log ("Screenshot by Hotkey (" + list [i].hotkey + ") could not be created! Camera not available.");
					}
				}
			}
		}
	}

	void activateCameraID(int id){
		for (int i = 0; i < list.Count; i++) {
			if (list[i].cam != null)
				list [i].cam.SetActive (false);
		}
		list[id].cam.SetActive (true);
	}

	public string getSaveDirectory(){
		string pickDirectory = customDirectory != "" ? customDirectory : "Screenshots";

		if (applicationPath) { // path to a safe location depending on the platform
			return Application.persistentDataPath + "/" + pickDirectory + "/";
		} else { // path to Unity project main folder
			return Directory.GetCurrentDirectory() + "/" + pickDirectory + "/";
		}
	}

	string checkSaveDirectory(){
		string directoryPath = getSaveDirectory ();

		if (!Directory.Exists(directoryPath)){
			Directory.CreateDirectory(directoryPath);
		}

		return directoryPath;
	}

	void initCutoutBoxStyle(){
		//if (cutoutBoxStyle == null) {
			cutoutBoxStyle = new GUIStyle (GUI.skin.box);

			int d = 16;

			Color[] c = new Color[d * d];
			for (int x = 0; x < d; x++) {
				for (int y = 0; y < d; y++) {
					if (x == 0 || x == d - 1 || y == 0 || y == d - 1) {
						c [x * d + y] = Color.white;
					} else {
						c [x * d + y] = new Color (1f, 1f, 1f, 0.1f);
					}
				}
			}

			Texture2D t = new Texture2D (d, d);
			t.SetPixels (c);
			t.Apply ();

			cutoutBoxStyle.normal.background = t;
		//}
	}

	void clampCutoutBox(){
		cutoutPosition.x = Mathf.Clamp (cutoutPosition.x, cutoutSize.x / 2f, (float)Screen.width - cutoutSize.x / 2f);
		cutoutPosition.y = Mathf.Clamp (cutoutPosition.y, cutoutSize.y / 2f, (float)Screen.height - cutoutSize.y / 2f);

		cutoutSize.x = Mathf.Clamp (cutoutSize.x, 0f, (float)Screen.width);
		cutoutSize.y = Mathf.Clamp (cutoutSize.y, 0f, (float)Screen.height);
	}

	Vector2 lastP;
	Vector2 lastS;
	float timer = 0f;

	void OnGUI () {
		if (lastP.x == cutoutPosition.x && lastP.y == cutoutPosition.y && lastS.x == cutoutSize.x && lastS.y == cutoutSize.y) {
			timer -= Time.deltaTime;
		} else {
			timer = 1f;
		}

		lastP = cutoutPosition;
		lastS = cutoutSize;

		if (timer <= 0f) {
			return;
		}
			
		if (captureMethod == CaptureMethod.Cutout) {
			initCutoutBoxStyle ();

			clampCutoutBox ();

			GUI.Box (new Rect (cutoutPosition.x - cutoutSize.x / 2f, cutoutPosition.y - cutoutSize.y / 2f, cutoutSize.x, cutoutSize.y), "", cutoutBoxStyle);
		}
	}

	public void CaptureCutoutVoid(int id){
		if (singleCamera) {
			activateCameraID(id);
		}

		StartCoroutine(CaptureCutout (id));
	}

	// create a more blurry screenshot if there are multiple cameras or no camera is found on the GameObject
	public IEnumerator CaptureCutout(int id){
		yield return new WaitForEndOfFrame();

		/*
		if (singleCamera) {
			activateCameraID(id);
		}
		*/

		string directoryName = checkSaveDirectory();
		string fileName = directoryName + getFileName (id);

		cutoutEmptyCheck ();
		clampCutoutBox ();

		var startX = (int)(cutoutPosition.x - cutoutSize.x / 2f);
		var startY = (int)((Screen.height - cutoutPosition.y) - cutoutSize.y / 2f);
		var width = (int)cutoutSize.x;
		var height = (int)cutoutSize.y;
		var tex = new Texture2D (width, height, TextureFormat.RGB24, false);
		
		tex.ReadPixels (new Rect(startX, startY, width, height), 0, 0);
		tex.Apply ();

		var bytes = tex.EncodeToPNG();
		Destroy(tex);
		
		File.WriteAllBytes(fileName, bytes);

		Debug.Log ("[ScreenshotCreator] Cutout Screenshot (" + width + "x" + height + " at " + startX + "," + startY + ") saved to: " + fileName);
	}

	void cutoutEmptyCheck(){
		if (cutoutSize.x <= 8f || cutoutSize.y <= 8f) {
			Debug.Log ("[ScreenshotCreator] A size of less than 8x8 pixels for Cutout has been detected!");
			if (Screen.width < 500 || Screen.height < 500) {
				Debug.Log ("[ScreenshotCreator] Reset to 500x500 pixels!");
				cutoutSize = new Vector2 (500f, 500f);
			} else {
				Debug.Log ("[ScreenshotCreator] Reset to " + Screen.width + "x" + Screen.height + " pixels!");
				cutoutSize = new Vector2 ((float)Screen.width, (float)Screen.height);
			}
		}
	}

	// create a more blurry screenshot if there are multiple cameras or no camera is found on the GameObject
	public void CaptureScreenshots(int id, bool fallback){
		if (singleCamera) {
			activateCameraID(id);
		}

		string directoryName = checkSaveDirectory();
		string fileName = directoryName + getFileName (id);

		Application.CaptureScreenshot (fileName, captureSizeMultiplier);

		if (fallback) {
			Debug.Log ("Fallback to Application.CaptureScreenshot because a GameObject without Camera (or Camera group) was used. Screenshot saved to: " + fileName);
		} else {
			Debug.Log ("Screenshot saved to: " + fileName);
		}
	}

	// create a sharp screenshot for a single Camera
	public void CaptureRenderTexture(Camera attachedCam, int id){
		for (int i = 0; i < list.Count; i++) {
			if (list[i].cam != null)
				list [i].cam.SetActive (false);
		}
		list[id].cam.SetActive (true);

		string directoryName = checkSaveDirectory();
		string fileName = directoryName + getFileName (id);

		int resWidth = (int)(attachedCam.pixelWidth * renderSizeMultiplier);
		int resHeight = (int)(attachedCam.pixelHeight * renderSizeMultiplier);

		RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);

		attachedCam.targetTexture = rt;
		Texture2D screenShot = new Texture2D (resWidth, resHeight, TextureFormat.RGB24, false);
		attachedCam.Render ();
		RenderTexture.active = rt;
		screenShot.ReadPixels (new Rect (0, 0, resWidth, resHeight), 0, 0);
		attachedCam.targetTexture = null;
		RenderTexture.active = null;
		DestroyImmediate (rt);
		byte[] bytes = screenShot.EncodeToPNG ();

		System.IO.File.WriteAllBytes (fileName, bytes);
		Debug.Log ("Screenshot saved to: " + fileName);
	}

	public string getFileName(int camID){
		string fileName = "";

		// custom name
		if (customName != "") {
			fileName += customName;
		} else {
			string dp = Application.dataPath;
			string[] s;
			s = dp.Split("/"[0]);
			fileName += s[s.Length - 2];
		}

		// include cam name
		if (includeCamName){
			fileName += "_";

			if (camID < 0 || camID >= list.Count || list[camID] == null || list[camID].cam == null) {
				fileName += "CameraName";
				lastCamID = 0;
			} else {
				fileName += list [camID].cam.name;
				lastCamID = camID;
			}
		}

		// include date
		if (includeDate) {
			fileName += "_";

			fileName += DateTime.Now.ToString ("yyyy-MM-dd-HH-mm-ss");
		}

		// include resolution
		if (includeResolution){
			fileName += "_";

			fileName += getResolution ();
		}

		// select filetype
		if (fileType == FileType.JPG) {
			fileName += ".jpg";
		} else if (fileType == FileType.PNG){
			fileName += ".png";
		}

		return fileName;
	}

	public string getResolution(){
		//return gameViewDimensions.width * superSize + "x" + gameViewDimensions.height * superSize;

		if (lastCam == null || list[lastCamID].cam != lastCam.gameObject) {
			if (list [lastCamID].cam != null) {
				lastCam = list [lastCamID].cam.GetComponentInChildren<Camera> ();
			} else {
				for (int i = 0; i < list.Count; i++) {
					if (list [i] == null || list [i].cam == null)
						continue;
					lastCam = list [i].cam.GetComponentInChildren<Camera> ();
					if (lastCam != null) {
						break;
					}
				}
			}
		}

		if (lastCam == null) {
			return "-x-";
		}
			
		if (captureMethod == CaptureMethod.RenderTexture) {
			return (int)(lastCam.pixelWidth * renderSizeMultiplier) + "x" + (int)(lastCam.pixelHeight * renderSizeMultiplier);
		}

		return lastCam.pixelWidth * captureSizeMultiplier + "x" + lastCam.pixelHeight * captureSizeMultiplier;
	}
}