﻿#region MIT.
// 
// Gorgon.
// Copyright (C) 2011 Michael Winsor
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// Created: Tuesday, July 19, 2011 8:55:06 AM
// 
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using GorgonLibrary.Native;
using D3D = SharpDX.Direct3D11;
using GI = SharpDX.DXGI;
using GorgonLibrary.Collections.Specialized;
using GorgonLibrary.Diagnostics;
using GorgonLibrary.Graphics.Properties;

namespace GorgonLibrary.Graphics
{
	/// <summary>
	/// Operators used for comparison operations.
	/// </summary>
	public enum ComparisonOperators
	{
		/// <summary>
		/// Unknown.
		/// </summary>
		Unknown = 0,
		/// <summary>
		/// Never pass the comparison.
		/// </summary>
		Never = 1,
		/// <summary>
		/// If the source data is less than the destination data, the comparison passes.
		/// </summary>
		Less = 2,
		/// <summary>
		/// If the source data is equal to the destination data, the comparison passes.
		/// </summary>
		Equal = 3,
		/// <summary>
		/// If the source data is less than or equal to the destination data, the comparison passes.
		/// </summary>
		LessEqual = 4,
		/// <summary>
		/// If the source data is greater than the destination data, the comparison passes.
		/// </summary>
		Greater = 5,
		/// <summary>
		/// If the source data is not equal to the destination data, the comparison passes.
		/// </summary>
		NotEqual = 6,
		/// <summary>
		/// If the source data is greater than or equal to the destination data, the comparison passes.
		/// </summary>
		GreaterEqual = 7,
		/// <summary>
		/// Always pass the comparison.
		/// </summary>
		Always = 8,
	}

	/// <summary>
	/// The primary object for the graphics sub system.
	/// </summary>
	/// <remarks>This interface is used to create all objects (buffers, shaders, etc...) that are to be used for graphics.  An interface is tied to a single physical video device, to use 
	/// multiple video devices, create additional graphics interfaces and assign the device to the <see cref="GorgonLibrary.Graphics.GorgonGraphics.VideoDevice">VideoDevice</see> property.
	/// <para>This object will enumerate video devices, monitor outputs (for multi-head adapters), and video modes for each of the video devices in the system upon creation.  These
    /// items are accessible from the <see cref="GorgonLibrary.Graphics.GorgonVideoDeviceEnumerator">GorgonVideoDeviceEnumerator</see> class. </para>
    /// <para>These objects can also be used in a deferred context.  This means that when a graphics object is deferred, it can be used in a multi threaded environment to allow set up of 
    /// a scene by recording commands sent to the video device for execution later on the rendering process.  This is handy where multiple passes for the same scene are required (e.g. a deferred renderer).</para>
	/// <para>Please note that this object requires Direct3D 11 (but not necessarily a Direct3D 11 video card) and at least Windows Vista Service Pack 2 or higher.  
	/// Windows XP and operating systems before it will not work, and an exception will be thrown if this object is created on those platforms.</para>
    /// <para>Deferred graphics contexts require a video device with a feature level of SM5 or better.</para>
	/// </remarks>
    public sealed class GorgonGraphics
        : IDisposable
    {
        #region Variables.
        private static readonly GorgonDisposableObjectCollection _trackedObjects = new GorgonDisposableObjectCollection();		// Tracked objects.
        private static bool _isDWMEnabled = true;						                                                        // Flag to indicate that the desktop window manager compositor is enabled.
        private static readonly bool _dontEnableDWM;						                                                    // Flag to indicate that we should not enable the DWM.
        private bool _disposed;                                                                                                 // Flag to indicate that the context was disposed.
        #endregion

        #region Properties.
        /// <summary>
        /// Property to set or return the D3D device context.
        /// </summary>
        internal D3D.DeviceContext Context
        {
            get;
            set;
        }

        /// <summary>
        /// Property to return the Direct3D 11 device object.
        /// </summary>
        internal D3D.Device D3DDevice
        {
            get;
            set;
        }

        /// <summary>
        /// Property to return the DX GI factory.
        /// </summary>
        internal GI.Factory1 GIFactory
        {
            get;
            set;
        }

        /// <summary>
        /// Property to return the DXGI adapter to use.
        /// </summary>
        internal GI.Adapter1 Adapter
        {
            get;
            set;
        }

        /// <summary>
        /// Property to set or return whether DWM composition is enabled or not.
        /// </summary>
        /// <remarks>This property will have no effect on systems that initially have the desktop window manager compositor disabled.</remarks>
        public static bool IsDWMCompositionEnabled
        {
            get
            {
                return _isDWMEnabled;
            }
            set
            {
                if (!value)
                {
                    if (_isDWMEnabled)
                    {
                        Win32API.DwmEnableComposition(0);
                        _isDWMEnabled = false;
                    }
                }
                else
                {
                    if ((!_isDWMEnabled) && (!_dontEnableDWM))
                    {
                        Win32API.DwmEnableComposition(1);
                        _isDWMEnabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Property to return the immediate graphics object that owns this context.
        /// </summary>
        public GorgonGraphics ImmediateContext
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return whether this context is deferred or not.
        /// </summary>
        public bool IsDeferred
        {
            get
            {
                return ImmediateContext != this;
            }
        }

        /// <summary>
        /// Property to return the input geometry interface.
        /// </summary>
        /// <remarks>
        /// The input interface covers items such as the vertex buffer, index buffer, bindings of the aforementioned buffers, the primitive type, etc...
        /// </remarks>
        public GorgonInputGeometry Input
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the interface for buffers.
        /// </summary>
        public GorgonBuffers Buffers
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the shader interface.
        /// </summary>
        /// <remarks>This is used to create shaders, create constant buffers and bind them to the pipeline.</remarks>
        public GorgonShaderBinding Shaders
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the current rasterizer states.
        /// </summary>
        public GorgonRasterizerRenderState Rasterizer
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the output merging interface.
        /// </summary>
        /// <remarks>This is responsible for setting blending states, depth/stencil states, creating render targets, etc...</remarks>
        public GorgonOutputMerger Output
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the textures interface.
        /// </summary>
        public GorgonTextures Textures
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to return the fonts interface.
        /// </summary>
        public GorgonFonts Fonts
        {
            get;
            private set;
        }
        
        /// <summary>
        /// Property to set or return the video device to use for this graphics interface.
        /// </summary>
        public GorgonVideoDevice VideoDevice
        {
            get;
            private set;
        }

        /// <summary>
        /// Property to set or return whether object tracking is disabled.
        /// </summary>
        /// <remarks>This will enable SharpDX's object tracking to ensure references are destroyed upon application exit.
        /// <para>The default value for DEBUG mode is TRUE, and for RELEASE it is set to FALSE.  Disabling object tracking will
        /// give a slight performance increase.</para>
        /// </remarks>
        public bool IsObjectTrackingEnabled
        {
            get
            {
                return SharpDX.Configuration.EnableObjectTracking;
            }
            set
            {
                SharpDX.Configuration.EnableObjectTracking = value;
            }
        }

        /// <summary>
        /// Property to set or return whether swap chains should reset their full screen setting on regaining focus.
        /// </summary>
        /// <remarks>
        /// This will control whether Gorgon will try to reacquire full screen mode when a full screen swap chain window regains focus.  When this is set to FALSE, and the window 
        /// containing the full screen swap chain loses focus, it will revert to windowed mode and remain in windowed mode.  When set to TRUE, it will try to reacquire full screen mode.
        /// <para>The default value for this is TRUE.  However, for a full screen multimonitor scenario, this should be set to FALSE.</para>
        /// </remarks>
        public bool ResetFullscreenOnFocus
        {
            get;
            set;
        }
        #endregion

        #region Methods.
        /// <summary>
        /// Function to clean up the categorized interfaces.
        /// </summary>
        private void DestroyInterfaces()
        {
            if (Fonts != null)
            {
                Fonts.CleanUp();
            }

            Fonts = null;

            if (Textures != null)
            {
                Textures.CleanUp();
            }

            Textures = null;

            if (Shaders != null)
            {
                Shaders.CleanUp();
            }

            Shaders = null;

            if (Output != null)
            {
                Output.CleanUp();
            }

            Output = null;

            if (Rasterizer != null)
            {
                Rasterizer.CleanUp();
            }
        }

        /// <summary>
        /// Function to create and initialize the various state objects.
        /// </summary>
        private void CreateStates()
        {
            // Create interfaces.
            Rasterizer = new GorgonRasterizerRenderState(this);
            Input = new GorgonInputGeometry(this);
            Shaders = new GorgonShaderBinding(this);
            Output = new GorgonOutputMerger(this);
            Textures = new GorgonTextures(this);
            Fonts = new GorgonFonts(this);
            Buffers = new GorgonBuffers(this);

            // Set default states.
            Rasterizer.States = GorgonRasterizerStates.CullBackFace;
            Output.BlendingState.States = GorgonBlendStates.DefaultStates;
            Output.DepthStencilState.States = GorgonDepthStencilStates.NoDepthStencil;

            // Initialize the shaders with default texture sampler settings.
            for (int i = 0; i < Shaders.VertexShader.TextureSamplers.Count; i++)
            {
                Shaders.VertexShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }

            for (int i = 0; i < Shaders.PixelShader.TextureSamplers.Count; i++)
            {
                Shaders.PixelShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }

            if (VideoDevice.SupportedFeatureLevel < DeviceFeatureLevel.SM4)
            {
                return;
            }

            for (int i = 0; i < Shaders.GeometryShader.TextureSamplers.Count; i++)
            {
                Shaders.GeometryShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }

            if (VideoDevice.SupportedFeatureLevel < DeviceFeatureLevel.SM5)
            {
                return;
            }

            for (int i = 0; i < Shaders.ComputeShader.TextureSamplers.Count; i++)
            {
                Shaders.ComputeShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }

            for (int i = 0; i < Shaders.HullShader.TextureSamplers.Count; i++)
            {
                Shaders.HullShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }

            for (int i = 0; i < Shaders.DomainShader.TextureSamplers.Count; i++)
            {
                Shaders.DomainShader.TextureSamplers[i] = GorgonTextureSamplerStates.DefaultStates;
            }
        }

        /// <summary>
        /// Function to retrieve a list of all swap chains that are currently full screen.
        /// </summary>
        /// <returns>The list of full screen swap chains.</returns>
        internal IEnumerable<GorgonSwapChain> GetFullScreenSwapChains()
        {
            return (from graphicsObj in _trackedObjects
                    let swap = graphicsObj as GorgonSwapChain
                    where (swap != null) && (!swap.Settings.IsWindowed)
                    select swap);
        }

        /// <summary>
        /// Function to add an object for tracking by the main Gorgon interface.
        /// </summary>
        /// <param name="trackedObject">Object to add.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="trackedObject"/> parameter is NULL (Nothing in VB.Net).</exception>
        /// <remarks>This allows Gorgon to track objects and destroy them upon <see cref="GorgonLibrary.Gorgon.Quit">termination</see>.</remarks>
        public void AddTrackedObject(IDisposable trackedObject)
        {
            if (trackedObject == null)
            {
                throw new ArgumentNullException("trackedObject");
            }

            _trackedObjects.Add(trackedObject);
        }

        /// <summary>
        /// Function to remove a tracked object from the Gorgon interface.
        /// </summary>
        /// <param name="trackedObject">Object to remove.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="trackedObject"/> parameter is NULL (Nothing in VB.Net).</exception>
        /// <remarks>This will -not- destroy the tracked object.</remarks>
        public void RemoveTrackedObject(IDisposable trackedObject)
        {
            if (trackedObject == null)
            {
                throw new ArgumentNullException("trackedObject");
            }

            _trackedObjects.Remove(trackedObject);
        }

        /// <summary>
        /// Function to a list of objects being tracked by a type value.
        /// </summary>
        /// <typeparam name="T">Type to search for.</typeparam>
        /// <returns>A list of objects that match the type.</returns>
        public IList<T> GetTrackedObjectsOfType<T>()
            where T : IDisposable
        {
            return (from trackedObject in _trackedObjects
                    where trackedObject is T
                    select (T)trackedObject).ToArray();
        }

        /// <summary>
        /// Function to create a deferred graphics context.
        /// </summary>
        /// <returns>A new graphics object as a deferred graphics context.</returns>
        /// <remarks>A deferred graphics context will allow for improved performance when used in a multi-threaded environment.  The deferred context takes rendering commands and queues them into a buffer 
        /// for execution later from the immediate context.  Use the <see cref="ExecuteDeferred"/> method to execute these commands from the immediate graphics object.
        /// <para>To use a deferred context the use needs to create a context with this method, then perform the rendering operations required.  Once the rendering operations are complete, then a call to 
        /// <see cref="FinalizeDeferred"/> is called and will return an object that will contain the command list.  Then a call to ExecuteDeferred using the command list object from the immediate context will 
        /// execute the commands.</para>
        /// <para>This method must be called from the immediate context.</para>
        /// <para>This method requires a video device with a feature level of SM5 or better.</para>
        /// </remarks>
        /// <exception cref="GorgonLibrary.GorgonException">Thrown when the deferred context could not be created.</exception>
        public GorgonGraphics CreateDeferredGraphics()
        {
            if (VideoDevice.SupportedFeatureLevel < DeviceFeatureLevel.SM5)
            {
                throw new GorgonException(GorgonResult.CannotCreate, string.Format(Resources.GORGFX_REQUIRES_SM, DeviceFeatureLevel.SM5));
            }
            if (IsDeferred)
            {
                throw new GorgonException(GorgonResult.CannotCreate, Resources.GORGFX_CANNOT_CREATE_CONTEXT_FROM_CONTEXT);
            }

            return new GorgonGraphics(this);
        }

        /// <summary>
        /// Function to finalize the deferred rendering context.
        /// </summary>
        /// <param name="restoreState">[Optional] TRUE to restore the state of the context after rendering, FALSE to keep it as-is.</param>
        /// <returns>An object containing the rendering commands to issue.</returns>
        /// <exception cref="System.NotSupportedException">Thrown when the current context is an immediate context.
        /// <para>-or-</para>
        /// <para>Thrown if the current video device does not have a feature level of SM5 or better.</para>
        /// </exception>
        /// <remarks>
        /// Use this method to finish recording of the rendering commands sent to a deferred context.  This method must be called from a deferred context.
        /// <para>This method requires a video device with a feature level of SM5 or better.</para>
        /// </remarks>
        public GorgonRenderCommands FinalizeDeferred(bool restoreState = false)
        {
#if DEBUG
            if (VideoDevice.SupportedFeatureLevel < DeviceFeatureLevel.SM5)
            {
                throw new NotSupportedException(string.Format(Resources.GORGFX_REQUIRES_SM, DeviceFeatureLevel.SM5));
            }
            if (!IsDeferred)
            {
                throw new NotSupportedException(Resources.GORGFX_CANNOT_USE_IMMEDIATE_CONTEXT);
            }
#endif

            var result = new GorgonRenderCommands(this, restoreState);

            AddTrackedObject(result);

            return result;
        }

        /// <summary>
        /// Function to execute rendering commands from a deferred context.
        /// </summary>
        /// <param name="commands">Commands to execute.</param>
        /// <param name="restoreState">TRUE to save and restore the context state before and after execution, FALSE to keep the state of the context as-is.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the <paramref name="commands"/> parameter is NULL (Nothing in VB.Net).</exception>
        /// <exception cref="System.NotSupportedException">Thrown when the current context is a deferred context.
        /// <para>-or-</para>
        /// <para>Thrown if the current video device does not have a feature level of SM5 or better.</para>
        /// </exception>
        /// <remarks>
        /// Use this method to execute previously recorded rendering commands on the immediate context.  This method must be called from the immediate context.
        /// <para>Using FALSE for <paramref name="restoreState"/> can avoid unncessary and inefficient state transitions.</para>
        /// <para>This method requires a video device with a feature level of SM5 or better.</para> 
        /// </remarks>
        public void ExecuteDeferred(GorgonRenderCommands commands, bool restoreState = false)
        {
#if DEBUG
            if (VideoDevice.SupportedFeatureLevel < DeviceFeatureLevel.SM5)
            {
                throw new NotSupportedException(string.Format(Resources.GORGFX_REQUIRES_SM, DeviceFeatureLevel.SM5));
            }
            if (IsDeferred)
            {
                throw new NotSupportedException(Resources.GORGFX_CANNOT_USE_DEFERRED_CONTEXT);
            }

            if (commands == null)
            {
                throw new ArgumentNullException("commands");
            }
#endif

            Context.ExecuteCommandList(commands.D3DCommands, restoreState);
        }
        #endregion

        #region Constructor/Destructor.
		/// <summary>
		/// Initializes the <see cref="GorgonGraphics"/> class.
		/// </summary>
		/// <param name="device">Video device to use.</param>
		/// <param name="featureLevel">The maximum feature level to support for the devices enumerated.</param>
		/// <exception cref="System.ArgumentException">Thrown when the <paramref name="featureLevel"/> parameter is invalid.</exception>
		/// <exception cref="GorgonLibrary.GorgonException">Thrown when Gorgon could not find any video devices that are Shader Model 5, or the down level interfaces (Shader Model 4, and lesser).
		/// <para>-or-</para>
		/// <para>Thrown if the operating system version is not supported.  Gorgon Graphics requires at least Windows Vista Service Pack 2 or higher.</para>
		/// </exception>
		/// <remarks>
		/// The <paramref name="device"/> parameter is the video device that should be used with Gorgon.  If the user passes NULL (Nothing in VB.Net), then the primary device will be used. 
        /// To determine the devices on the system, check the <see cref="GorgonLibrary.Graphics.GorgonVideoDeviceEnumerator">GorgonVideoDeviceEnumerator</see> class.  The primary device will be the first device in this collection. 
		/// <para>The user may pass in a feature level to the featureLevel parameter to limit the feature levels available.  Note that the feature levels imply all feature levels up until the feature level passed in, for example, passing <c>DeviceFeatureLevel.SM4</c> will only allow functionality 
		/// for both Shader Model 4, and Shader Model 2/3 capable video devices, while DeviceFeatureLevel.SM4_1 will include Shader Model 4 with a 4.1 profile and Shader model 2/3 video devices.</para>
		/// <para>If a feature level is not supported by the hardware, then Gorgon will not use that feature level.  That is, passing a SM5 feature level with a SM4 card will only use a SM4 feature level.  If the user omits the feature level (in one of the constructor 
		/// overloads), then Gorgon will use the best available feature level for the video device being used.</para>
		/// </remarks>
		public GorgonGraphics(GorgonVideoDevice device, DeviceFeatureLevel featureLevel)
		{
        	ResetFullscreenOnFocus = true;
            ImmediateContext = this;

            if (featureLevel == DeviceFeatureLevel.Unsupported)
            {
                throw new ArgumentException(Resources.GORGFX_FEATURE_LEVEL_UNKNOWN);
            }

            if (GorgonComputerInfo.OperatingSystemVersion.Major < 6)
            {
                throw new GorgonException(GorgonResult.CannotCreate, Resources.GORGFX_INVALID_OS);
            }

            Gorgon.Log.Print("Gorgon Graphics initializing...", LoggingLevel.Simple);

#if DEBUG
            if (!SharpDX.Configuration.EnableObjectTracking)
            {
                SharpDX.Configuration.EnableObjectTracking = true;
            }
#else
			SharpDX.Configuration.EnableObjectTracking = false;
#endif

            if (device == null)
            {
                if (GorgonVideoDeviceEnumerator.VideoDevices.Count == 0)
                {
                    GorgonVideoDeviceEnumerator.Enumerate(false, false);
                }

                // Use the first device in the list.
                device = GorgonVideoDeviceEnumerator.VideoDevices[0];
            }

            VideoDevice = device;

            var D3DDeviceData = VideoDevice.GetDevice(VideoDevice.VideoDeviceType, featureLevel);

            // Create the DXGI factory for the video device.
            GIFactory = D3DDeviceData.Item1;
            Adapter = D3DDeviceData.Item2;
            D3DDevice = D3DDeviceData.Item3;

		    Context = D3DDevice.ImmediateContext;
            Context.ClearState();
            VideoDevice.Graphics = ImmediateContext;

            CreateStates();

            Gorgon.AddTrackedObject(this);

		    Gorgon.Log.Print("Gorgon Graphics initialized.", LoggingLevel.Simple);
		}

		/// <summary>
		/// Initializes the <see cref="GorgonGraphics"/> class.
		/// </summary>
		/// <param name="device">Video device to use.</param>
		/// <exception cref="GorgonLibrary.GorgonException">Thrown when Gorgon could not find any video devices that are Shader Model 5, or the down level interfaces (Shader Model 4, and lesser).
		/// <para>-or-</para>
		/// <para>Thrown if the operating system version is not supported.  Gorgon Graphics requires at least Windows Vista Service Pack 2 or higher.</para>
		/// </exception>
		/// <remarks>
		/// The <paramref name="device"/> parameter is the video device that should be used with Gorgon.  If the user passes NULL (Nothing in VB.Net), then the primary device will be used. 
        /// To determine the devices on the system, check the <see cref="GorgonLibrary.Graphics.GorgonVideoDeviceEnumerator">GorgonVideoDeviceEnumerator</see> class.  The primary device will be the first device in this collection. 
		/// </remarks>
		public GorgonGraphics(GorgonVideoDevice device)
			: this(device, DeviceFeatureLevel.SM5)
		{
		}

		/// <summary>
		/// Initializes the <see cref="GorgonGraphics"/> class.
		/// </summary>
		/// <param name="featureLevel">The maximum feature level to support for the devices enumerated.</param>
		/// <exception cref="System.ArgumentException">Thrown when the <paramref name="featureLevel"/> parameter is invalid.</exception>
		/// <exception cref="GorgonLibrary.GorgonException">Thrown when Gorgon could not find any video devices that are Shader Model 5, or the down level interfaces (Shader Model 4, and lesser).
		/// <para>-or-</para>
		/// <para>Thrown if the operating system version is not supported.  Gorgon Graphics requires at least Windows Vista Service Pack 2 or higher.</para>
		/// </exception>
		/// <remarks>The user may pass in a feature level to the featureLevel parameter to limit the feature levels available.  Note that the feature levels imply all feature levels up until the feature level passed in, for example, passing <c>DeviceFeatureLevel.SM4</c> will only allow functionality 
		/// for both Shader Model 4, and Shader Model 2/3 capable video devices, while DeviceFeatureLevel.SM4_1 will include Shader Model 4 with a 4.1 profile and Shader model 2/3 video devices.
		/// <para>If a feature level is not supported by the hardware, then Gorgon will not use that feature level.</para>
		/// </remarks>
		public GorgonGraphics(DeviceFeatureLevel featureLevel)
			: this(null, featureLevel)
		{
		}

		/// <summary>
		/// Initializes the <see cref="GorgonGraphics"/> class.
		/// </summary>
		/// <exception cref="GorgonLibrary.GorgonException">Thrown when Gorgon could not find any video devices that are Shader Model 5, or the down level interfaces (Shader Model 4, and lesser).
		/// <para>-or-</para>
		/// <para>Thrown if the operating system version is not supported.  Gorgon Graphics requires at least Windows Vista Service Pack 2 or higher.</para>
		/// </exception>
		public GorgonGraphics()
			: this(null, DeviceFeatureLevel.SM5)
		{
		}

		/// <summary>
		/// Initializes the <see cref="GorgonGraphics"/> class.
		/// </summary>
		static GorgonGraphics()
		{
			Win32API.DwmIsCompositionEnabled(out _isDWMEnabled);

			if (!_isDWMEnabled)
			{
				_dontEnableDWM = true;
			}
		}

        /// <summary>
        /// Initializes a new instance of the <see cref="GorgonGraphics"/> class.
        /// </summary>
        /// <param name="graphics">The immediate graphics context.</param>
        internal GorgonGraphics(GorgonGraphics graphics)
        {
            // If we have an immediate context, then assume this one is deferred.
            Context = new D3D.DeviceContext(graphics.D3DDevice);
            Context.ClearState();
            ImmediateContext = graphics;

            VideoDevice = graphics.VideoDevice;
            GIFactory = graphics.GIFactory;
            Adapter = graphics.Adapter;
            D3DDevice = graphics.D3DDevice;

            CreateStates();

            Gorgon.AddTrackedObject(this);
        }
        #endregion

        #region IDisposable Members
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Gorgon.Log.Print("Gorgon Graphics Context shutting down...", LoggingLevel.Simple);

                if (Context != null)
                {
                    Context.ClearState();
                }

                _trackedObjects.ReleaseAll();

                DestroyInterfaces();
                
                // Only clean up the context if the context is deferred.
                if (IsDeferred)
                {
                    if (Context != null)
                    {
                        Context.Dispose();
                    }

                    Context = null;
                }
                else
                {
                    Gorgon.Log.Print("Removing D3D11 Device object...", LoggingLevel.Verbose);

                    // Destroy the video device interface.
                    if (D3DDevice != null)
                    {
                        D3DDevice.Dispose();
                        D3DDevice = null;
                    }

                    if (Adapter != null)
                    {
                        Adapter.Dispose();
                        Adapter = null;
                    }

                    if (GIFactory != null)
                    {
                        GIFactory.Dispose();
                        GIFactory = null;
                    }

                    if (VideoDevice != null)
                    {
                        VideoDevice.Graphics = null;
                    }

                    Gorgon.Log.Print("Removing DXGI factory interface...", LoggingLevel.Verbose);
                    if (GIFactory != null)
                    {
                        GIFactory.Dispose();
                        GIFactory = null;
                    }
                }

                // Remove us from the object tracker.
                Gorgon.RemoveTrackedObject(this);

                Gorgon.Log.Print("Gorgon Graphics Context shut down successfully", LoggingLevel.Simple);
            }

            _disposed = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
