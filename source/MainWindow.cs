﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using OpenTK.Audio.OpenAL;

using RocketNet;

namespace OpenTkConsole
{
	static class Error
	{
		static public bool checkGLError(string place)
		{
			bool errorFound = false;
			while ( GL.GetError() != ErrorCode.NoError)
			{
				Console.WriteLine("GL error in " + place);
				errorFound = true;
			}
			return errorFound;
		}

		static public bool checkALError(string place)
		{
			bool errorFound = false;
			while ( AL.GetError() != ALError.NoError)
			{
				Console.WriteLine("AL error in " + place);
				errorFound = true;
			}
			return errorFound;
		}
	}
	
    public sealed class MainWindow : GameWindow
    {
        private bool running;

        // Syncing
        public Track redColorTrack;
        public Track greenColorTrack;
        public Device syncDevice;

        private int bpm;
        private int rowsPerBeat;

        private float songLength;
        private int syncRow;

        bool paused;
		bool spaceDown;

        bool useSync;

		IScene testScene;
        Stopwatch timer;

        public MainWindow()
            : base(854, 480, 
                  GraphicsMode.Default,
                  "OpenTK party",
                  GameWindowFlags.Default,
                  DisplayDevice.Default,
                  3, 
                  0,
                  GraphicsContextFlags.ForwardCompatible)
        {
            Title += "OpenGL version: " + GL.GetString(StringName.Version);
			Console.WriteLine("OpenTK initialized. OpenGL version: " + GL.GetString(StringName.Version));
            base.TargetUpdateFrequency = 120.0;
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(0, 0, Width, Height);
        }

        protected override void OnLoad(EventArgs e)
        {
            CursorVisible = true;
            running = true;
			paused = true;
			spaceDown = false;




			// Audio
			initAudio();



			// SYNC
			useSync = false;
			loadSyncer();
			bpm = 120;
			rowsPerBeat = 4;
			songLength = 5.0f; // seconds


			// Materials and scenes
			// Pass syncer to scenes.
			try
			{
				MaterialManager.init("../data/materials/");

				testScene = new EmptyScene();
				testScene.loadScene();
			}
			catch (Exception exception)
			{
				Console.WriteLine("Caugh exception when loading scene" + exception.Message);
			}


			// Timing
			timer = new Stopwatch();
            timer.Start();
        }

        public bool demoPlaying()
        {
            return running;
        }

        public void SetRowFromEditor(int row)
        {
            syncRow = row;
        }

        public void PauseFromEditor(bool pause)
        {
            paused = pause;
        }

        void Sync()
        {
            float secondsElapsed = 0.0f;
            // update sync values only when playing
            if (!paused)
            {
                // Calculate sync row.
                long elapsedMS = timer.ElapsedMilliseconds;
                secondsElapsed = (float)(elapsedMS / (float)1000);
                float minutesElapsed = secondsElapsed / 60.0f;

                if (secondsElapsed > songLength)
                {
                    // loop around;
                    timer.Restart();
                }

                float beatsElapsed = (float)bpm * minutesElapsed;
                float rowsElapsed = rowsPerBeat * beatsElapsed;
                float floatRow = rowsElapsed;
                int currentRow = (int)Math.Floor(floatRow);

                syncRow = currentRow;
            }

            bool updateOk = syncDevice.Update(syncRow);
            if (!updateOk)
            {
                connectSyncer();
            }

            Title = $"Seconds: {secondsElapsed:0} Row: {syncRow}";
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            HandleKeyboard();
			if (useSync)
			{
				Sync();
			}
        }

        private void HandleKeyboard()
        {
            var keyState = Keyboard.GetState();

			if (keyState.IsKeyDown(Key.Space))
			{
				spaceDown = true;
			}
			
			if (spaceDown && keyState.IsKeyUp(Key.Space))
			{
				spaceDown = false;
				paused = !paused;
			}
			
            if (keyState.IsKeyDown(Key.Escape))
            {
				running = false;
                timer.Stop();

				cleanupAndExit();
               
            }

			// Pass input to scene

			// Take scene number from track

			testScene.updateScene(keyState);

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            if (!running)
            {
                return;
            }

			// Take scene number from track.
			// Draw that scene

            Color4 backColor;
            backColor.A = 1.0f;
            backColor.R = redColorTrack.GetValue(syncRow);
            backColor.G = greenColorTrack.GetValue(syncRow);
            backColor.B = 0.8f;
            GL.ClearColor(backColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			// Draw models
			testScene.drawScene();

			// Scene drawing ends
			
            if (Error.checkGLError("OnRenderFrame"))
            {
                running = false;
            }

            SwapBuffers();
        }
		
		void loadSyncer()
		{
			syncDevice = new Device("test", false);
            redColorTrack = syncDevice.GetTrack("redColor");
            greenColorTrack = syncDevice.GetTrack("greenColor");

			if (useSync)
			{
				connectSyncer();
				
				syncDevice.IsPlaying = demoPlaying;
				syncDevice.Pause = PauseFromEditor;
				syncDevice.SetRow = SetRowFromEditor;
			}
		}
		
		void connectSyncer()
		{
			try
			{
				syncDevice.Connect();

			} catch(System.Net.Sockets.SocketException socketE)
			{

				Console.WriteLine("Socket exception: " + socketE.Message);
				running = false;
			}
		}

		void initAudio()
		{
			

			

			IntPtr nullDevice = System.IntPtr.Zero;
			IList<string> allDevices = Alc.GetString(nullDevice, AlcGetStringList.DeviceSpecifier);
			foreach(string s in allDevices)
			{
				Console.WriteLine("OpenAL device " + s);
			}

			// Open preferred device
			ContextHandle alContext;
			IntPtr ALDevicePtr = Alc.OpenDevice(null);
			if (ALDevicePtr != null)
			{
				int[] deviceAttributes = null;
				alContext = Alc.CreateContext(ALDevicePtr, deviceAttributes);
				Alc.MakeContextCurrent(alContext);
			}
			else
			{
				Console.WriteLine("Could not get AL device");
				return;
			}

			string alRenderer = AL.Get(ALGetString.Renderer);
			string alVendor = AL.Get(ALGetString.Vendor);
			string alVersion = AL.Get(ALGetString.Version);

			Console.WriteLine("OpenAL Renderer {0}  Vendor {1}  Version {2}", alRenderer, alVendor, alVersion);


			Error.checkALError("initAudio");
			int alBuffer = AL.GenBuffer();
			Error.checkALError("initAudio genBuffer");

			
			int frequenzy = 44100;

			// Buffer data
			bool dataisVorbis = false;

			string vorbisEXTName = "AL_EXT_vorbis";
			if (AL.IsExtensionPresent(vorbisEXTName) && dataisVorbis)
			{
				Console.WriteLine("AL can use vorbis");
				IntPtr vorbisBuffer = System.IntPtr.Zero;
				int vorbisSize = 0;
				AL.BufferData(alBuffer, ALFormat.VorbisExt, vorbisBuffer, vorbisSize, frequenzy);
			}
			else
			{
				// Load wav
				
				FileStream audioFile = File.Open("../data/music/bosca.wav", FileMode.Open, FileAccess.Read);
				long wavSize = audioFile.Length;
				byte[] audioContents = new byte[wavSize];

				audioFile.Read(audioContents, 0, (int)wavSize);
				IntPtr wavBuffer = Marshal.AllocHGlobal(audioContents.Length);
				Marshal.Copy(audioContents, 0, wavBuffer, audioContents.Length);

				AL.BufferData(alBuffer, ALFormat.Stereo16, wavBuffer, (int)wavSize, frequenzy);
				Marshal.FreeHGlobal(wavBuffer);
				audioFile.Close();
			}

			Error.checkALError("initAudio bufferAudio");

			int alSource = AL.GenSource();
			Error.checkALError("initAudio genSource");

			// Attach buffer to source.
			AL.Source(alSource, ALSourcei.Buffer, alBuffer);

			// Set listener and source to same place
			Vector3 listenerPos = new Vector3(0, 0, 0);
			Vector3 sourcePos = new Vector3(0, 0, 0);
			AL.Listener(ALListener3f.Position, ref listenerPos);
			AL.Source(alSource, ALSource3f.Position, ref sourcePos);

			// Play buffer
			AL.SourcePlay(alSource);
		}

		void shutDownAudio()
		{
			ContextHandle alContext = Alc.GetCurrentContext();
			IntPtr alDevice = Alc.GetContextsDevice(alContext);
			ContextHandle emptyContext = ContextHandle.Zero;
			Alc.MakeContextCurrent(emptyContext);
			Alc.DestroyContext(alContext);
			Alc.CloseDevice(alDevice);
		}

		void cleanupAndExit()
		{
			syncDevice.Dispose();
			shutDownAudio();
			Exit();
		}
    }

}
