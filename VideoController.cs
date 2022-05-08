using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine.Video;
using System.Text;
using System.Linq;
using UnityEngine.UI;
using System.IO;

// VaM is on Unity 2018.1.9f1 at this point (https://docs.unity3d.com/2018.1/Documentation/ScriptReference/Video.VideoPlayer.html)
// If you want to create a solution file based on Meshed's with autocomplete, you'll need to add an additional reference to UnityEngine.VideoModule.dll from your VaM folder using "Browse"
// This will run fine in VaM if you don't - it's just nice having the compiler know what a "VideoPlayer" is.
// TLDR Hints: CreatePlayer() is where we make the VideoPlayer, CreateUI() is where we add the UI elements.

namespace VamSander {
	public class VideoController : MVRScript
	{
		class ScreenObject
		{
			public VideoPlayer panel;
			public AudioSource audioSource;
			public MeshFilter meshFilter;
			public Vector3[] flatVerts;
			public float lastCurvature;
			public float easeTimer;
			public Vector3 easeLocalEndPos;
			public Vector3 easeLocalStartPos;
			public bool once; // stop at end of playback
			public VideoFile currentVideoFile;
		}

		class VideoFile
		{
			public string path;
			public int screenIndex;
			public bool noAudio;
			public VideoFile(string file, int screen = -1, bool audio = true) { path = file; screenIndex = screen; noAudio = !audio; }
		}

		List<ScreenObject> activeScreens = new List<ScreenObject>();

		string[] validFileTypes = new string[] { ".asf", ".avi", ".dv", ".m4v", ".mov", ".mp4", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv" };

		string pathRoot;
		GameObject videoNode;
		string lastMode = "";
		float aspectRatioMultiplier = 9.0f / 16.0f; // multiply width by this to get height
		bool lastFreezeState;
		float easeInDuration = 1.0f;
		float easeInScalar = 1.0f;
		float rotateSpeed = 360.0f / 30.0f; // degrees per second
		bool pauseVideo;
		bool pauseVideoFirstScreen;

		List<VideoFile> videoFiles = new List<VideoFile>();
		List<VideoFile> toPlay = new List<VideoFile>();

		// JSON storage
		JSONStorableString jsonVideoFolder;
		JSONStorableString jsonFileList;
		JSONStorableFloat jsonNumScreensX;
		JSONStorableFloat jsonNumScreensY;
		JSONStorableFloat jsonVolume;
		JSONStorableFloat jsonOffset;
		JSONStorableFloat jsonCurvature;
		JSONStorableStringChooser jsonMode;
		JSONStorableStringChooser jsonAspectRatio;
		JSONStorableFloat jsonScale;
		JSONStorableBool jsonLoopAll;
		JSONStorableBool jsonVolumeAll;
		JSONStorableBool jsonEaseIn;
		JSONStorableBool jsonRotateY;
		JSONStorableBool jsonPlayAlphabetical;
		// triggers
		JSONStorableString jsonPlayFile;
		JSONStorableString jsonAddFile;

		UIDynamicSlider screensSliderX;
		UIDynamicSlider screensSliderY;
		UIDynamicButton resetButton;

		bool initialised = false;

		public override void Init() {
			try {
				// put init code in here
				SuperController.LogMessage("VideoController Loaded");
				pathRoot = GetVamRootFolder();

				CreateUI();

				GameObject root = GetOurRootObject();
				if (!root)
					return;

				lastFreezeState = SuperController.singleton.freezeAnimation;

				videoNode = new GameObject("Videos");
				videoNode.transform.SetParent(root.transform, false);
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		// in the absence of enums, we use this as our mode keys
		const string modeWall = "Wall";
		const string modeCylinder = "Cylinder";
		const string modeDome = "Dome";
		const string modeArch = "Arch";
		const string mode5GridH = "5GridH";
		const string mode5GridV = "5GridV";
		const string mode13Grid = "13Grid";
		readonly List<string> layoutModeNames = new List<string>(new string[] { modeWall, modeCylinder, modeDome, modeArch, mode5GridH, mode5GridV, mode13Grid });
		// aspect ratios
		readonly List<string> aspectRatios = new List<string>(new string[] { "19:10", "16:9", "4:3", "1:1", "9:16", "10:9" });

		void MakeIntSlider(UIDynamicSlider slider)
		{
			slider.slider.wholeNumbers = true;
			slider.rangeAdjustEnabled = false;
			slider.autoSetQuickButtons = false;
			slider.ConfigureQuickButtons(-1.0f, -2.0f, -5.0f, -10.0f, 1.0f, 2.0f, 5.0f, 10.0f);
		}

		void CreateUI()
		{
			// folder browse
			UIDynamicButton browseButton = CreateButton("Browse To Folder -->", false);
			browseButton.button.onClick.AddListener(BrowseButtonCallback);

			// spacer
			UIDynamic spacer = CreateSpacer(false);
			spacer.height = 32;

			// refresh list
			UIDynamicButton refreshButton = CreateButton("Refresh List -->", false);
			refreshButton.button.onClick.AddListener(RefreshButtonCallback);

			// spacer
			spacer = CreateSpacer(false);
			spacer.height = 32;

			// Play/Reset
			resetButton = CreateButton("Play", false);
			resetButton.button.onClick.AddListener(ResetButtonCallback);

			// spacer
			spacer = CreateSpacer(false);
			spacer.height = 16;

			// layout modes
			jsonMode = new JSONStorableStringChooser("Layout Mode", layoutModeNames, "Wall", "Layout", LayoutModeCallback);
			RegisterStringChooser(jsonMode);
			CreateScrollablePopup(jsonMode, false);

			// screens across
			jsonNumScreensX = new JSONStorableFloat("Number Of Screens X", 1, DimsCallback, 0, 8, true, true);
			RegisterFloat(jsonNumScreensX);
			screensSliderX = CreateSlider(jsonNumScreensX, false);
			MakeIntSlider(screensSliderX);

			// radius
			jsonOffset = new JSONStorableFloat("Distance", 5, OffsetCallback, 0, 100, true, true);
			RegisterFloat(jsonOffset);
			CreateSlider(jsonOffset, false);

			// Aspect Ratio
			jsonAspectRatio = new JSONStorableStringChooser("Aspect Ratio", aspectRatios, aspectRatios[1], "Aspect Ratio", AspectRatioCallback);
			RegisterStringChooser(jsonAspectRatio);
			CreateScrollablePopup(jsonAspectRatio, false);

			// loop all videos instead of shuffling
			jsonLoopAll = new JSONStorableBool("Loop Mode", false);
			RegisterBool(jsonLoopAll);
			CreateToggle(jsonLoopAll, false);

			jsonEaseIn = new JSONStorableBool("Animate In", false, EaseInCallback);
			RegisterBool(jsonEaseIn);
			CreateToggle(jsonEaseIn, false);

			jsonRotateY = new JSONStorableBool("Rotate", false, RotateCallback);
			RegisterBool(jsonRotateY);
			CreateToggle(jsonRotateY, false);

			// Right Side

			// register the path string
			jsonVideoFolder = new JSONStorableString("Video Path", pathRoot);
			RegisterString(jsonVideoFolder);
			CreateTextField(jsonVideoFolder, true).height = 8;

			// right hand side video list
			jsonFileList = new JSONStorableString("Video List Box", "");
			UIDynamicTextField fileListBox = CreateTextField(jsonFileList, true);
			fileListBox.height = 300;

			// screens up
			jsonNumScreensY = new JSONStorableFloat("Number Of Screens Y", 1, DimsCallback, 1, 5, true, true);
			RegisterFloat(jsonNumScreensY);
			screensSliderY = CreateSlider(jsonNumScreensY, true);
			MakeIntSlider(screensSliderY);

			// curvature
			jsonCurvature = new JSONStorableFloat("Curvature", 0, CurvatureCallback, 0, 1, true, true);
			RegisterFloat(jsonCurvature);
			CreateSlider(jsonCurvature, true);

			// Scale
			jsonScale = new JSONStorableFloat("Screen Size", 5.0f, SizeCallback, 0.1f, 50.0f, false, true);
			RegisterFloat(jsonScale);
			CreateSlider(jsonScale, true);

			// volume
			jsonVolume = new JSONStorableFloat("Audio Volume", 0.5f, VolumeCallback, 0, 1, true, true);
			RegisterFloat(jsonVolume);
			CreateSlider(jsonVolume, true);

				// Play audio from all screens or just the first
			jsonVolumeAll = new JSONStorableBool("Audio on All Screens", false, VolumeAllCallback);
			RegisterBool(jsonVolumeAll);
			CreateToggle(jsonVolumeAll, true);

			

			// sort alphabetically not randomly
			jsonPlayAlphabetical = new JSONStorableBool("Alphabetical Order (off is random)", false, AlphabeticalCallback);
			RegisterBool(jsonPlayAlphabetical);
			CreateToggle(jsonPlayAlphabetical, true);

		

			aspectRatioMultiplier = ExtractAspectRatio(jsonAspectRatio.val);
			SetPathTextBoxText();
			EnableScreenCountControls();

			// External Triggers
			// named video, plays once, doesn't get added to the list
			jsonPlayFile = new JSONStorableString("Play Once", "E.g.  c:\\MyVids\\Video1.mpg", PlayVideoAtPathCallback);
			RegisterString(jsonPlayFile);

			// add videos to list
			jsonAddFile = new JSONStorableString("Add Files", "Simple Example: c:\\MyVids\\Video1.mpg\nAdvanced Example: c:\\MyVids\\Video1.mpg, screen:2, audio:false\nOne video per line, screen and audio are optional", AddVideoCallback);
			RegisterString(jsonAddFile);

			// play next all
			JSONStorableAction jsonPlayNext = new JSONStorableAction("PlayNext", PlayNextCallback);
			RegisterAction(jsonPlayNext);
			
			// play next
			JSONStorableAction jsonPlayNextFirstScreen = new JSONStorableAction("PlayNextFirstScreen", PlayNextFirstScreenCallback);
			RegisterAction(jsonPlayNextFirstScreen);

			// play/pause all
			JSONStorableAction jsonPlayPause = new JSONStorableAction("PlayPause", PlayPauseCallback);
			RegisterAction(jsonPlayPause);			
			
			// play/pause 
			JSONStorableAction jsonPlayPauseFirstScreen = new JSONStorableAction("PlayPauseFirstScreen", PlayPauseFirstScreenCallback);
			RegisterAction(jsonPlayPauseFirstScreen);

			// stop all
			JSONStorableAction jsonStop = new JSONStorableAction("Stop", StopCallback);
			RegisterAction(jsonStop);

			// stop
			JSONStorableAction jsonStopFirstScreen = new JSONStorableAction("StopFirstScreen", StopFirstScreenCallback);
			RegisterAction(jsonStopFirstScreen);

			// refresh
			JSONStorableAction jsonRefresh = new JSONStorableAction("Refresh/Clear", RefreshCallback);
			RegisterAction(jsonRefresh);
		}

		void SetPathTextBoxText()
		{
			StringBuilder list = new StringBuilder();
			//list.Append("Video Folder.\n" + jsonVideoFolder.val + "\n" + videoFiles.Count.ToString() + " videos found in folder.\n");
			if (videoFiles.Count == 0)
				list.Append("Please Browse to a folder containing videos, valid file types are : " + String.Join(",", validFileTypes));
			foreach (VideoFile vid in videoFiles)
			{
				string str = vid.path;
				string file = str;
				if (str.StartsWith(pathRoot))
					file = str.Substring(pathRoot.Length);
				list.Append(file + " " + vid.screenIndex + " " + !vid.noAudio + "\n");
			}
			jsonFileList.val = list.ToString();
			resetButton.button.interactable = (videoFiles.Count > 0);
			if ((videoFiles.Count > 0) && (jsonNumScreensX.val == 0))
				jsonNumScreensX.val = 1.1f; // we hold this at zero till we have videos
		}

		const string pathContraction = "<VAM>/";

		// sanitise any paths inside the VAM folder - this makes scenes using the plugin more portable
		string SanitisePath(string path)
		{
			path = path.Trim().TrimEnd('\n', '\r').Trim();
			if (path.StartsWith(pathRoot, StringComparison.InvariantCultureIgnoreCase))
			{
				string localPath = path.Substring(pathRoot.Length, path.Length - pathRoot.Length);
				localPath = localPath.TrimStart('/', '\\');
				path = pathContraction + localPath; // e.g. <VAM>/vids/1.mpg
			}
			return path;
		}

		// replace pathContraction with pathRoot
		string ExpandPath(string path)
		{
			if (path.StartsWith(pathContraction))
				path = pathRoot + "/" + path.Substring(pathContraction.Length, path.Length - pathContraction.Length); // e.g. c:/VAM/vids/1.mpg
			return path;
		}

		// System.IO is banned - Unity to the rescue!
		// this returns the path without a trailing slash
		string GetVamRootFolder()
		{
			string ret = Application.dataPath;
			// manually strip "\VaM_Data" as System.IO is banned
			ret = ret.TrimEnd('/', '\\'); // just in case
			int finalFSlash = ret.LastIndexOf('/');
			int finalBSlash = ret.LastIndexOf('\\');
			if ((finalFSlash != -1) && (finalFSlash > finalBSlash))
				ret = ret.Substring(0, finalFSlash);
			else if (finalFSlash != -1)
				ret = ret.Substring(0, finalBSlash);
			return ret;
		}

		bool IsValidFiletype(string path)
		{
			return validFileTypes.Any(x => path.EndsWith(x));
		}

		List<VideoFile> ReadFilesAtPath(string path)
		{
			List<string> fileList = SuperController.singleton.GetFilesAtPath(path).ToList<string>();
			if ((fileList == null) || (fileList.Count == 0))
				return new List<VideoFile>(); // not sure if GetFilesAtPath can return null
			List<string> filteredList = new List<string>();
			foreach (string str in fileList)
			{
				if (IsValidFiletype(str))
					filteredList.Add(str);
			}
			filteredList.Sort();
			List<VideoFile> ret = new List<VideoFile>();
			for (int i = 0; i < filteredList.Count; i++)
				ret.Add(new VideoFile(SanitisePath(filteredList[i])));
			return ret;
		}

		// returns height / width
		float ExtractAspectRatio(string str)
		{
			string[] values = str.Split(':');
			if (values.Length != 2)
			{
				LogError("Bad aspect ratio " + str);
				return 9.0f / 16.0f;
			}
			float x = 16, y = 9;
			float.TryParse(values[0], out x);
			float.TryParse(values[1], out y);
			return y / x;
		}

		void LogError(string str)
		{
			SuperController.LogError(str);
		}

		void LogMessage(string str)
		{
			SuperController.LogMessage(str);
		}

		// the root of the controller hierarchy
		GameObject GetOurRootObject()
		{
			FreeControllerV3 controller = GetMainController();
			if (!controller)
			{
				LogError("No controller - you need to attach this to an object in the scene");
				return null;
			}
			return controller.gameObject;
		}

		VideoFile PopVideo(int screenIndex)
		{
			if ((videoFiles != null) && (videoFiles.Count>0))
			{
				if (toPlay.Count == 0)
					Shuffle();
				if (toPlay.Count > 0)
				{
					int index = -1;
					// first check for first matching screenIndex
					for (int i = 0; i<toPlay.Count;i++)
					{
						if (toPlay[i].screenIndex == screenIndex)
						{
							index = i;
							break;
						}
					}
					if (index == -1)
					{
						// none matching index - find first that isn't hardcoded
						for (int i = 0; i < toPlay.Count; i++)
						{
							if (toPlay[i].screenIndex == -1)
							{
								index = i;
								break;
							}
						}
					}
					if (index == -1)
						index = 0; // just play first
					VideoFile ret = toPlay[index];
					toPlay.RemoveAt(index);
					return ret;
				}
			}
			return new VideoFile("");
		}

		void Shuffle()
		{
			if (videoFiles != null)
			{
				if (jsonPlayAlphabetical.val)
					toPlay = new List<VideoFile>(videoFiles).OrderBy(x => x.path).ToList();
				else
					toPlay = new List<VideoFile>(videoFiles).OrderBy(x => UnityEngine.Random.value).ToList();
			}
		}

		void SetEaseInOnPlayer(VideoPlayer vp)
		{
			// locate it
			ScreenObject scr = activeScreens.Find(x => x.panel == vp);
			if (scr==null)
			{
				LogError("Failed to find screen to ease in");
				return;
			}
			scr.easeTimer = easeInDuration;
		}

		int ScreenFromPanel(VideoPlayer vp)
		{
			for (int i=0;i<activeScreens.Count;i++)
			{
				if (activeScreens[i].panel == vp)
					return i;
			}
			return -1;
		}

		void EndReached(VideoPlayer vp)
		{
			int screenIndex = ScreenFromPanel(vp);
			if (screenIndex == -1)
				return; // shouldn't happen, but ignore if it does
			ScreenObject screen = activeScreens[screenIndex];
			if (screen.once)
				return;
			vp.isLooping = jsonLoopAll.val;
			if (jsonLoopAll.val)
				return;
			if (jsonEaseIn.val)
				SetEaseInOnPlayer(vp);
			screen.currentVideoFile = PopVideo(screenIndex);
			vp.url = ExpandPath(screen.currentVideoFile.path);
			if(jsonVolumeAll.val || screenIndex == 0)
			{
				ResetVolume(screen);
			}
			else
			{
				MuteVolume(screen);
			}
			vp.Play();
		}


		VideoPlayer CreatePlayer(string path,PrimitiveType shape = PrimitiveType.Plane)
		{
			VideoPlayer player;

			path = ExpandPath(path);
			// we could create our own bespoke mesh object, but meh!
			GameObject playerObject = GameObject.CreatePrimitive(shape);
			playerObject.transform.SetParent(videoNode.transform);
			// quads are created in the x,y plane, the -ve z side is visible
			// planes are created as 10x10 quads (11x11 verts) in the x,z plane, the faces are wound so +ve y is visible
			if (shape == PrimitiveType.Plane)
			{
				SwapZY(playerObject); // we physically move the verts on the plane to make it vertical, this means we are dealing with the same orientation regardless of type
			}
			// We don't need the collider - if people need these to be collidable they can place a physics Atom (or comment out these 3 lines)
			Collider col = playerObject.GetComponent<Collider>();
			if (col)
				Destroy(col);
			Renderer renderer = playerObject.GetComponent<Renderer>();
			if (!renderer)
			{
				LogError("No renderer found on primitive");
				return null;
			}

			Shader shader = Shader.Find("Sprites/Default"); // Sprites default is a good 2-sided, cheap, unlit shader and VaM includes it
			if (shader != null)
				renderer.material = new Material(shader);
			else
				LogError("Failed to find Spites/Default");
			if (!path.Contains("_nosound"))
			{
				AudioSource audioSource = playerObject.AddComponent<AudioSource>();
				audioSource.spatialBlend = 1.0f;
				float dist = jsonOffset.val;
				audioSource.minDistance = dist / 2.0f;
				audioSource.maxDistance = dist * 5.0f;
				audioSource.volume = jsonVolume.val;
				audioSource.dopplerLevel = 0; // we'll avoid doppler as it makes the audio choppy when moving the camera too far
			}

			player = playerObject.AddComponent<VideoPlayer>();
			player.url = path;
			player.aspectRatio = VideoAspectRatio.FitInside;
			player.isLooping = jsonLoopAll.val;
			player.loopPointReached += EndReached;
			
			player.prepareCompleted += PostPrepareCallback;
			player.Prepare();

			return player;
		}

		void PostPrepareCallback (VideoPlayer player)
		{
			// SetScale(player, 1.0f);
			UpdateLayout();
			player.Play();
			if (SuperController.singleton.freezeAnimation)
				player.Pause();
		}

		// this is really just intended to make Plane primitives more like quad primitives - we cheekily invert x,y at the same time as swapping z and y
		// and scale the vertex positions down
		void SwapZY(GameObject shape)
		{
			MeshFilter mf = shape.GetComponent<MeshFilter>();
			Mesh mesh;
			if ((!mf) || (!(mesh = mf.mesh)))
			{
				LogError("VideoController: SwapZY, no mesh");
				return;
			}
			Vector3[] verts = mesh.vertices;
			for (int i = 0; i < verts.Length; i++)
			{
				verts[i].x = -verts[i].x / 10.0f;
				verts[i].y = -verts[i].z / 10.0f;
				verts[i].z = 0;
			}
			mesh.vertices = verts;
			mesh.RecalculateBounds();
		}

		const float imaginaryRadius = 2.0f;

		// this is only going to work for objects with plenty of subdivision = really only intended for Plane
		void SetCurvature(ScreenObject screen,float curvature)
		{
			if (screen.flatVerts == null)
				return;
			Vector3[] verts = new Vector3[screen.flatVerts.Length];
			screen.flatVerts.CopyTo(verts, 0);

			// we project the plane onto the surface of a sphere using an imaginary centre in -ve z (all these verts are in localspace at z=0)
			// it won't preserve area perfectly - but it's good enough (Mercator vs Equal Area - go Greenland programmers)
			Vector3 centre = new Vector3(0, 0, -imaginaryRadius); // assumes flatMesh is at z=0
			for (int i = 0; i < verts.Length; i++)
			{
				Vector3 v = verts[i];
				Vector3 vec = v - centre; // vector from centre to vertex
				float dist = vec.magnitude; // distance to that point
				vec = vec.normalized;
				Vector3 onSurface = centre + vec * (imaginaryRadius * (imaginaryRadius / dist)); // point on sphere (bunched up at far coords) (kind of like our map of the world)
				verts[i] = Vector3.Lerp(v, onSurface, curvature); // LERP from flat to curved
			}
			screen.meshFilter.mesh.vertices = verts;
			screen.meshFilter.mesh.RecalculateBounds();
		}

		void SetVolumeOfAllScreens(float val)
		{
			Boolean first = true;

			foreach (ScreenObject screen in activeScreens)
			{
				if (screen.audioSource)
				{
					if(jsonVolumeAll.val || first)
					{
						screen.audioSource.volume = (screen.currentVideoFile.noAudio) ? 0 : val;
					}
					else
					{
						screen.audioSource.volume = 0 ;
					}
				}

				first = false;
			}
		}

		void ResetVolume(ScreenObject screen)
		{
			screen.audioSource.volume = (screen.currentVideoFile.noAudio) ? 0 : jsonVolume.val;
		}		
		
		void MuteVolume(ScreenObject screen)
		{
			screen.audioSource.volume = 0 ;
		}

		void PlayPauseAll(bool play, bool firstScreenOnly = false)
		{
			foreach (ScreenObject screen in activeScreens)
			{
				if (play)
					screen.panel.Play();
				else
					screen.panel.Pause();

				if(firstScreenOnly)
				{
					break;
				}
			}
		}
		
		void StopAll(bool firstScreenOnly = false)
		{
			foreach (ScreenObject screen in activeScreens)
			{
				screen.panel.Stop();

				if(firstScreenOnly)
				{
					break;
				}
			}
		}
		
		void DeleteAllScreens()
		{
			// clear the videoNode
			foreach (Transform t in videoNode.transform)
				Destroy(t.gameObject);
			// reset the screen list
			activeScreens.Clear();
		}

		Vector3 CalcScreenScale()
		{
			// we use an area preservation approach based on the square of size
			// solving area = scale^2 and width * height = area and height/width = aspectRatio => width = sqrt(area/aspectRatio), height = area/width
			float area = jsonScale.val * jsonScale.val;
			float width = Mathf.Sqrt(area / aspectRatioMultiplier);
			float height = area / width;
			float depth = jsonScale.val; // bit of a cheat - mainly needed for curvature
			return new Vector3(width, height, depth);
		}

		void SetScale(VideoPlayer player, float multiplier)
		{
			Vector3 scale = CalcScreenScale();

			float aspectRatio = 1f;
			if (player.texture != null)
				aspectRatio = (float)player.texture.width / (float)player.texture.height;
			// check for NaN
			if (aspectRatio != aspectRatio)
				aspectRatio = 1f;

			player.transform.localScale = new Vector3(scale.y * multiplier * aspectRatio, scale.y * multiplier, scale.z * multiplier);
		}

		Vector3 GetScale(VideoPlayer player, float multiplier)
		{
			Vector3 scale = CalcScreenScale();

			float aspectRatio = 1f;
			if (player.texture != null)
				aspectRatio = (float)player.texture.width / (float)player.texture.height;
			// check for NaN
			if (aspectRatio != aspectRatio)
				aspectRatio = 1f;

			return new Vector3(scale.y * multiplier * aspectRatio, scale.y * multiplier, scale.z * multiplier);
		}

		// Circumference at y on a sphere, -ve return value if outside sphere
		float CircumferenceAtHeight(float y, float radius)
		{
			// x = sqrt(radius^2 - y^2) - x is radius of bisected circle at this new point
			float r2 = radius * radius;
			float y2 = y * y;
			if (y2 > r2)
				return -1;
			float x = Mathf.Sqrt(r2 - y2);
			return 2.0f * Mathf.PI * x;
		}

		// We obey the width value at the base - user agency (if they want them crowded or sparse we let them)
		// at higher y values (higher rings) we reduce x by the ratio of the new circumference (they'll still be crowded or sparse if that's what they want)
		// They can easily fix the density by adjusting either the distance or size
		int CalcSizeOfDomeRing(int width,int y)
		{
			if (y == 0)
				return width; // easy out
			Vector3 size = CalcScreenScale();
			float baseCircumference = CircumferenceAtHeight(size.y * 0.5f, jsonOffset.val);
			// isoceles triangle
			float sine = size.y / (2.0f * jsonOffset.val);
			if (Mathf.Abs(sine) > 1.0f)
				return -1;
			float radiansBetween = Mathf.Asin(sine) * 2.0f;
			float radians = ((float)y + 0.5f) * radiansBetween;
			if (radians > Mathf.PI / 2.0f)
				return -1; // >90 degrees
			float yPos = Mathf.Sin(radians) * jsonOffset.val;
			float circ = CircumferenceAtHeight(yPos, jsonOffset.val);
			if (circ <= 0)
				return -1; // y is above sphere, they ran out of space - shouldn't really happen by this point
			return (int)(circ * width / baseCircumference); // can return 0
		}

		VideoPlayer GetScreen(int i)
		{
			if (i < activeScreens.Count)
			{
				activeScreens[i].once = false; // reset
				return activeScreens[i].panel;
			}
			if (i > activeScreens.Count)
			{
				LogError("logic error: a non incremental screen request was made. Requested:" + i + " activeScreens=" + activeScreens.Count);
				return null;
			}
			VideoFile video = PopVideo(i);
			ScreenObject screen = new ScreenObject();
			screen.currentVideoFile = video;
			screen.panel = CreatePlayer(video.path);
			screen.meshFilter = screen.panel.GetComponent<MeshFilter>();
			if (screen.meshFilter == null)
			{
				LogError("VideoController: We made a video player that has no mesh attached");
				return null;
			}
			screen.audioSource = screen.panel.GetComponent<AudioSource>();
			screen.flatVerts = screen.meshFilter.mesh.vertices;
			screen.lastCurvature = 0;
			screen.easeTimer = 0;
			if(jsonVolumeAll.val || i == 0)
			{
				ResetVolume(screen);
			}
			else
			{
				MuteVolume(screen);
			}
			activeScreens.Add(screen);
			return screen.panel;
		}

		// clear out any screens max or above
		void ClearScreensAbove(int max)
		{
			for (int i=max;i<activeScreens.Count;i++)
				Destroy(activeScreens[i].panel.gameObject);
			activeScreens.RemoveRange(max, activeScreens.Count - max);
		}

		// set of functions to set the position/scale/orientation and number of screens - resizing is conservative, meaning we keep whatever is playing
		// still playing where possible

		// x,y grid
		void SetWallPositions()
		{
			int i = 0;
			for (int y = 0; y < (int)jsonNumScreensY.val; y++)
			{
				for (int x = 0; x < (int)jsonNumScreensX.val; x++,i++)
				{
					VideoPlayer player = GetScreen(i);
					SetScale(player, 1.0f);
					//Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
					// Vector3 scale = CalcScreenScale();
					Vector3 scale = GetScale(player, 1.0f);
					Vector2 size = new Vector2(scale.x, scale.y);
					// ScreenX/Y are whole screen positions
					float screenY = (float)y + 0.5f;
					float screenX = (float)x - (jsonNumScreensX.val / 2.0f) + 0.5f;
					Vector3 pos = new Vector3(screenX * size.x, screenY * size.y, jsonOffset.val);
					player.gameObject.transform.localPosition = pos;
					player.transform.localRotation = Quaternion.identity;
				}
			}
			ClearScreensAbove(i);
		}

		void SetArchPositions()
		{
			int i = 0;
			for (int y = 0; y < (int)jsonNumScreensY.val; y++)
			{
				for (int x = 0; x < (int)jsonNumScreensX.val; x++,i++)
				{
					VideoPlayer player = GetScreen(i);
					SetScale(player, 1.0f);
					//Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
					// Vector3 scale = CalcScreenScale();
					Vector3 scale = GetScale(player, 1.0f);
					Vector2 size = new Vector2(scale.x, scale.y);
					// ScreenX/Y are whole screen positions
					float screenY = (float)y + 0.5f;
					float screenX = (float)x - (jsonNumScreensX.val / 2.0f) + 0.5f;
					Vector3 pos = new Vector3(screenX * size.x, 0, jsonOffset.val);
					// using isoceles with two sides jsonOffset and the other facing side size.y (sin(theta/2) = y / 2*jsonOffet)
					float sine = size.y / (2.0f * jsonOffset.val);
					sine = Mathf.Clamp(sine, -1.0f, 1.0f); // clamp - will look wrong but could happen on small radius large y
					float degreesBetween = Mathf.Rad2Deg * Mathf.Asin(sine) * 2.0f;
					pos = Quaternion.Euler(Vector3.left * degreesBetween * screenY) * pos; // rotate around local x axis
					player.gameObject.transform.localPosition = pos;
					// orient to face the root node
					Vector3 lookAt = videoNode.transform.position + videoNode.transform.right * screenX * size.x;
					Vector3 worldPos = player.gameObject.transform.position;
					player.gameObject.transform.forward = worldPos - lookAt;
				}
			}
			ClearScreensAbove(i);
		}

		void SetCylinderPositions()
		{
			int i = 0;
			for (int y = 0; y < (int)jsonNumScreensY.val; y++)
			{
				for (int x = 0; x < (int)jsonNumScreensX.val; x++,i++)
				{
					VideoPlayer player = GetScreen(i);
					SetScale(player, 1.0f);
					//Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
					// Vector3 scale = CalcScreenScale();
					Vector3 scale = GetScale(player, 1.0f);
					Vector2 size = new Vector2(scale.x, scale.y);
					// ScreenX/Y are whole screen positions
					float screenY = (float)y + 0.5f;
					float screenX = x;
					float degreesBetween = 360.0f / jsonNumScreensX.val;
					Vector3 pos = new Vector3(0, screenY * size.y, jsonOffset.val);
					pos = Quaternion.Euler(Vector3.up * degreesBetween * screenX) * pos;
					player.gameObject.transform.localPosition = pos;
					Vector3 lookAt = videoNode.transform.position;
					Vector3 worldPos = player.gameObject.transform.position;
					lookAt.y = worldPos.y; // don't lean inwards
					player.gameObject.transform.forward = worldPos - lookAt;
				}
			}
			ClearScreensAbove(i);
		}

		// This one is a bit more complex so I've moved some of the heavy lifting into functions to break it down
		void DomePosition()
		{ 
			int i = 0;
			Vector3 lookAt = videoNode.transform.position;
			for (int y = 0; y < (int)jsonNumScreensY.val; y++)
			{
				int numAtRing = CalcSizeOfDomeRing((int)jsonNumScreensX.val, y);
				if (numAtRing <= 0)
					break; // ran out of space given the requirements
				float degreesXOffset = (float)y *360.0f / jsonNumScreensY.val;
				for (int x = 0; x < numAtRing; x++)
				{
					VideoPlayer player = GetScreen(i);
					SetScale(player, 1.0f);
					//Vector2 size = player.transform.localScale;
					// Vector3 scale = CalcScreenScale();
					Vector3 scale = GetScale(player, 1.0f);
					Vector2 size = new Vector2(scale.x, scale.y);
					float degreesX = (360.0f * (float)i / (float)numAtRing) + degreesXOffset;
					Vector3 pos = new Vector3(0, 0.5f * size.y, jsonOffset.val); // base position
					// rotate up - using isoceles with two sides jsonOffset and the other facing side size.y (sin(theta/2) = size.y / 2*jsonOffet)
					float sine = size.y / (2.0f * jsonOffset.val);
					if (Mathf.Abs(sine) > 1.0f)
						break; // failsafe
					float radiansBetween = Mathf.Asin(sine) * 2.0f;
					float radians = ((float)y) * radiansBetween;
					if (radians > Mathf.PI / 2.0f)
						break; // failsafe
					float degreesY = Mathf.Rad2Deg * radians;
					pos = Quaternion.Euler(Vector3.left * degreesY) * pos; // rotate around local x axis
					pos = Quaternion.Euler(Vector3.up * degreesX) * pos; // rotate around vertical
					player.gameObject.transform.localPosition = pos;
					Vector3 worldPos = player.gameObject.transform.position;
					player.gameObject.transform.forward = worldPos - lookAt;
					i++;
				}
			}
			ClearScreensAbove(i);
		}

		void Set5GridVerticalPositions()
		{
			for (int i = 0; i < 5; i++)
			{
				VideoPlayer player = GetScreen(i);
				float sizeMultiplier = (i == 0) ? 1.0f : 0.5f;
				SetScale(player, sizeMultiplier);
				
				// Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
				
				// Vector3 scale = CalcScreenScale();
				Vector3 scale = GetScale(player, sizeMultiplier);
				Vector2 size = new Vector2(scale.x, scale.y);

				// SuperController.LogMessage("Set5GridVerticalPositions " + i  ); 
				
				Vector3 pos;
				if (i == 0)
				{ // normal sized centre for first screen
					pos = new Vector3(0, size.y, jsonOffset.val);
				}
				else
				{
					// top or bottom row
					float screenX = ((float)(i & 1) - 0.5f);
					float screenY = (i >= 3) ? 3.5f : 0.5f;
					pos = new Vector3(screenX * size.x, screenY * size.y, jsonOffset.val);
				}
				player.gameObject.transform.localPosition = pos;
				player.transform.localRotation = Quaternion.identity;
			}
			ClearScreensAbove(5);
		}

		void Set5GridHorizontalPositions()
		{
			for (int i = 0; i < 5; i++)
			{
				VideoPlayer player = GetScreen(i);
				float sizeMultiplier = (i == 0) ? 1.0f : 0.5f;
				SetScale(player, sizeMultiplier);
				
				// Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
				
				// Vector3 scale = CalcScreenScale();
				Vector3 scale = GetScale(player, sizeMultiplier);
				Vector2 size = new Vector2(scale.x, scale.y);
				
				// SuperController.LogMessage("Set5GridHorizontalPositions " + i  );

				Vector3 pos;
				if (i == 0)
				{ // normal sized centre for first screen
					pos = new Vector3(0, size.y * 0.5f, jsonOffset.val);
				}
				else
				{
					// left or right columms
					float screenX = ((i & 1) > 0) ? 1.5f : -1.5f;
					float screenY = (i >= 3) ? 1.5f : 0.5f;
					pos = new Vector3(screenX * size.x, screenY * size.y, jsonOffset.val);
				}
				player.gameObject.transform.localPosition = pos;
				player.transform.localRotation = Quaternion.identity;
			}
			ClearScreensAbove(5);
		}

		void Set13GridPositions()
		{
			for (int i = 0; i < 13; i++)
			{
				VideoPlayer player = GetScreen(i);
				float sizeMultiplier = (i == 0) ? 1.0f : 0.5f;
				SetScale(player, sizeMultiplier);
				
				// Vector2 size = player.transform.localScale; // x,y size (ignoring curvature)
	
				// Vector3 scale = CalcScreenScale();
				Vector3 scale = GetScale(player, sizeMultiplier);
				Vector2 size = new Vector2(scale.x, scale.y);

				Vector3 pos;
				if (i == 0)
				{ // normal sized centre for first screen
					pos = new Vector3(0, size.y, jsonOffset.val);
				}
				else
				{
					float screenX = 0;
					float screenY = 0;
					// easier to hand code this one BL -> TR
					switch (i)
					{
						case 1:
							screenX = -1.5f;
							screenY = 0.5f;
							break;
						case 2:
							screenX = -0.5f;
							screenY = 0.5f;
							break;
						case 3:
							screenX = 0.5f;
							screenY = 0.5f;
							break;
						case 4:
							screenX = 1.5f;
							screenY = 0.5f;
							break;
						case 5: // 2nd row
							screenX = -1.5f;
							screenY = 1.5f;
							break;
						case 6:
							screenX = 1.5f;
							screenY = 1.5f;
							break;
						case 7: // 3rd row
							screenX = -1.5f;
							screenY = 2.5f;
							break;
						case 8:
							screenX = 1.5f;
							screenY = 2.5f;
							break;
						case 9: // top row
							screenX = -1.5f;
							screenY = 3.5f;
							break;
						case 10:
							screenX = -0.5f;
							screenY = 3.5f;
							break;
						case 11:
							screenX = 0.5f;
							screenY = 3.5f;
							break;
						case 12:
							screenX = 1.5f;
							screenY = 3.5f;
							break;
					}
					pos = new Vector3(screenX * size.x, screenY * size.y, jsonOffset.val);
					player.transform.localRotation = Quaternion.identity;
				}
				player.gameObject.transform.localPosition = pos;
			}
			ClearScreensAbove(13);
		}

		// set the position of the screens
		void UpdateLayout()
		{
			if (!initialised)
				return;
			switch (jsonMode.val)
			{
				case modeWall:
					SetWallPositions();
					break;
				case modeCylinder:
					SetCylinderPositions();
					break;
				case modeArch:
					SetArchPositions();
					break;
				case modeDome:
					DomePosition();
					break;
				case mode5GridV:
					Set5GridVerticalPositions();
					break;
				case mode5GridH:
					Set5GridHorizontalPositions();
					break;
				case mode13Grid:
					Set13GridPositions();
					break;
				default:
					LogError("Unknown mode " + jsonMode.val);
					break;
			}
			// apply the curvatures if they've changed and do some housekeeping
			for (int i = 0; i < activeScreens.Count; i++)
			{
				VideoPlayer player = activeScreens[i].panel;
				if (activeScreens[i].lastCurvature != jsonCurvature.val)
				{
					SetCurvature(activeScreens[i], jsonCurvature.val);
					activeScreens[i].lastCurvature = jsonCurvature.val;
				}
				Vector3 easeLocalForwards = player.transform.localRotation * Vector3.forward;
				activeScreens[i].easeLocalEndPos = player.transform.localPosition;
				activeScreens[i].easeLocalStartPos = player.transform.localPosition + easeLocalForwards * jsonOffset.val * easeInScalar;
			}
		}

		void EnableScreenCountControls()
		{
			bool enabled;
			switch (jsonMode.val)
			{
				case modeWall:
				case modeCylinder:
				case modeDome:
				case modeArch:
					enabled = true;
					break;
				default:
					enabled = false;
					break;
			}
			screensSliderX.gameObject.SetActive(enabled);
			screensSliderY.gameObject.SetActive(enabled);
		}

		// Debug functions 

		/*
		string GetGameObjectPath(GameObject obj)
		{
			string path = "/" + obj.name;
			while (obj.transform.parent != null)
			{
				obj = obj.transform.parent.gameObject;
				path = "/" + obj.name + path;
			}
			return path;
		}

		GameObject FindRoot()
		{
			GameObject ret = containingAtom.gameObject;
			SuperController.LogMessage(ret.name);
			while (ret.transform.parent)
			{
				ret = ret.transform.parent.gameObject;
				//SuperController.LogMessage(ret.name);
			}
			return ret;
		}

		void DumpHierarchy(GameObject node, string path, StringBuilder ret)
		{
			if (!node.activeSelf)
				return;
			path += "/" + node.name;
			Component[] comps = node.GetComponents<Component>();
			string compStr = " ";
			foreach (Component c in comps)
				compStr += c.GetType().ToString() + ",";
			ret.Append(path + compStr + "\n");
			foreach (Transform t in node.gameObject.transform)
				DumpHierarchy(t.gameObject, path, ret);
		}

		// post load coroutine
		IEnumerator InitDeferred()
		{
			while (SuperController.singleton.isLoading)
				yield return 0;
			yield return 0;
		}
		*/

		// UI callbacks

		void BrowseButtonCallback()
		{
			SuperController.singleton.GetDirectoryPathDialog(FolderChosenCallback, jsonVideoFolder.val, null, false);
		}

		void FolderChosenCallback(string path)
		{
			jsonVideoFolder.val = path;
			videoFiles = ReadFilesAtPath(path);
			SetPathTextBoxText();
		}

		void RefreshButtonCallback()
		{
			pauseVideo = false;
			pauseVideoFirstScreen = false;
			videoFiles = ReadFilesAtPath(jsonVideoFolder.val);
			SetPathTextBoxText();
			Shuffle();
		}

		void LayoutModeCallback(JSONStorableStringChooser js)
		{
			EnableScreenCountControls();
			if (lastMode != jsonMode.val)
			{
				UpdateLayout();
				lastMode = jsonMode.val;
			}
		}

		void AspectRatioCallback(JSONStorableStringChooser js)
		{
			aspectRatioMultiplier = ExtractAspectRatio(jsonAspectRatio.val);
			UpdateLayout();
		}

		void EaseInCallback(JSONStorableBool js)
		{
			// nothing we need to do here - we'll obey the state change next time a video ends
		}

		// don't randomly shuffle
		void AlphabeticalCallback(JSONStorableBool js)
		{
			// this will pick up the new value of the bool
			Shuffle();
		}

		void RotateCallback(JSONStorableBool js)
		{
			if ((!js.val) && (videoNode))
			{
				videoNode.transform.localRotation = Quaternion.identity; // reset when turned off
			}
		}

		void CurvatureCallback(JSONStorableFloat jf)
		{
			UpdateLayout();
		}

		void VolumeCallback(JSONStorableFloat jf)
		{
			SetVolumeOfAllScreens(jsonVolume.val);
		}
		void VolumeAllCallback(JSONStorableBool js)
		{
			SetVolumeOfAllScreens(jsonVolume.val);
		}

		void SizeCallback(JSONStorableFloat jf)
		{
			UpdateLayout();
		}

		void OffsetCallback(JSONStorableFloat jf)
		{
			UpdateLayout();
		}

		void DimsCallback(JSONStorableFloat jf)
		{
			UpdateLayout();
		}

		// Play/Reset
		void ResetButtonCallback()
		{
			DeleteAllScreens();
			UpdateLayout();
		}

		// play video at path trigger
		void PlayVideoAtPathCallback(string path)
		{
			if ((jsonPlayFile == null) || (String.IsNullOrEmpty(jsonPlayFile.val)))
				return;

			// annoyingly VaM won't send us this trigger unless the string has changed - so we force it back to empty regardless. The first check above will stop this recursing.
			jsonPlayFile.val = "";
			if (jsonNumScreensX.val == 0)
				return;

			VideoFile vid = ParseTriggerString(path);
			if (vid.screenIndex>=activeScreens.Count)
			{
				LogError("Can't Play Once on screen " + vid.screenIndex + " as the screen does not exist. " + path);
				return;
			}

			int screenIndex = (vid.screenIndex == -1) ? 0 : vid.screenIndex;
			VideoPlayer panel = GetScreen(screenIndex);
			activeScreens[screenIndex].once = true;
			panel.url = ExpandPath(vid.path);
			panel.Play();
			if (SuperController.singleton.freezeAnimation)
				panel.Pause();
		}

		VideoFile ParseTriggerString(string line)
		{
			string[] split = line.Split(',');
			if ((split.Length == 0) || (!IsValidFiletype(split[0])))
			{
				LogError("Add Files: Couldn't find valid video path in " + line);
				return null;
			}
			string path = SanitisePath(split[0]);
			// optional args
			int screenIndex = -1;
			bool playAudio = true;
			for (int i = 1; i < split.Length; i++)
			{
				int indexOf = split[i].IndexOf(screenNumberString);
				if (indexOf >= 0)
				{
					if (!Int32.TryParse(split[i].Substring(indexOf + screenNumberString.Length), out screenIndex))
						LogError("Failed to read screen number in " + line);
					continue;
				}
				indexOf = split[i].IndexOf(audioString);
				if (indexOf >= 0)
				{
					string arg = split[i].Substring(indexOf + audioString.Length);
					if (arg.ToLower().Contains("false"))
						playAudio = false;
				}
			}
			return new VideoFile(path, screenIndex, playAudio);
		}

		int FindExistingFile(string path)
		{
			for (int i=0;i<videoFiles.Count;i++)
			{
				if (videoFiles[i].path == path)
					return i;
			}
			return -1;
		}

		const string screenNumberString = "screen:";
		const string audioString = "audio:";

		void AddVideoCallback(string files)
		{
			if ((jsonAddFile == null) || (String.IsNullOrEmpty(jsonAddFile.val)))
				return;

			// annoyingly VaM won't send us this trigger unless the string has changed - so we force it back to empty regardless. The first check above will stop this recursing.
			jsonAddFile.val = "";

			// extract the lines
			string[] lines = files.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

			foreach (string line in lines)
			{
				VideoFile vid = ParseTriggerString(line);
				if (vid == null)
					continue; // assume parser has reported error
				int index = FindExistingFile(vid.path);
				if (index == -1)
				{
					videoFiles.Add(vid);
					index = videoFiles.Count - 1;
				}
			}
			SetPathTextBoxText();
		}

		void PlayNextCallback()
		{
			pauseVideo = false;
			pauseVideoFirstScreen = false;
			ResetButtonCallback();
		}

		void PlayNextFirstScreenCallback()
		{
			pauseVideoFirstScreen = false;
			VideoPlayer player = GetScreen(0);
			EndReached(player);
		}

		void PlayPauseCallback()
		{
			pauseVideo = !pauseVideo;
			pauseVideoFirstScreen = pauseVideo;
			PlayPauseAll(!pauseVideo);
		}
		void PlayPauseFirstScreenCallback()
		{
			pauseVideoFirstScreen = !pauseVideoFirstScreen;
			PlayPauseAll(!pauseVideoFirstScreen, true);
		}
		
		void StopCallback()
		{
			StopAll();
		}		
		void StopFirstScreenCallback()
		{
			StopAll(true);
		}

		void RefreshCallback()
		{
			RefreshButtonCallback();
		}

		// Unity Lifecycle Events

		void Start() {
			try {
				// nothing currently, but I'll leave it here
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		void Update() {
			try {
				if (!initialised)
				{
					initialised = true;
					videoFiles = ReadFilesAtPath(jsonVideoFolder.val);
					SetPathTextBoxText();
					UpdateLayout();
				}
				bool freezeState = SuperController.singleton.freezeAnimation;
				if (freezeState != lastFreezeState)
				{
					PlayPauseAll(!freezeState);
					lastFreezeState = freezeState;
				}
				if (freezeState)
					return;
				if ((jsonRotateY.val) && (videoNode))
				{
					videoNode.transform.localRotation = videoNode.transform.localRotation * Quaternion.AngleAxis(Time.deltaTime * rotateSpeed, Vector3.up);
				}
				if (jsonEaseIn.val)
				{
					foreach (ScreenObject scr in activeScreens)
					{
						if (scr.easeTimer > 0)
						{
							scr.easeTimer -= Time.deltaTime;
							if (scr.easeTimer <= 0)
								scr.easeTimer = 0;
							float easeDistance = jsonOffset.val * easeInScalar;
							float t = scr.easeTimer / easeInDuration; // 1 .. 0
							t = t * t; // make the ease quadratic
							scr.panel.transform.localPosition = Vector3.Lerp(scr.easeLocalEndPos, scr.easeLocalStartPos, t);
						}
					}
				}
			}
			catch (Exception e) {
				SuperController.LogError("Exception caught: " + e);
			}
		}

		void OnDestroy() {
			if (videoNode)
				Destroy(videoNode);
		}

	}
}