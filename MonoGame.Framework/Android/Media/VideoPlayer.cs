// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Android.Widget;
using Microsoft.Xna.Framework.Graphics;

#if API_LEVEL_11_OR_HIGHER
using OpenTK.Graphics.ES20;

using ShaderType = OpenTK.Graphics.ES20.All;
using PixelInternalFormat = OpenTK.Graphics.ES20.All;
using PixelFormat = OpenTK.Graphics.ES20.All;
using PixelType = OpenTK.Graphics.ES20.All;
using TextureTarget = OpenTK.Graphics.ES20.All;
using TextureParameterName = OpenTK.Graphics.ES20.All;
using TextureWrapMode = OpenTK.Graphics.ES20.All;
using TextureMinFilter = OpenTK.Graphics.ES20.All;
using TextureMagFilter = OpenTK.Graphics.ES20.All;
using FramebufferTarget = OpenTK.Graphics.ES20.All;
using FramebufferAttachment = OpenTK.Graphics.ES20.All;
using GetPName = OpenTK.Graphics.ES20.All;
using TextureUnit = OpenTK.Graphics.ES20.All;
using EnableCap = OpenTK.Graphics.ES20.All;
using VertexAttribPointerType = OpenTK.Graphics.ES20.All;
using BeginMode = OpenTK.Graphics.ES20.All;
#endif

namespace Microsoft.Xna.Framework.Media
{
    public sealed class VideoPlayer : IDisposable
    {
		private Video  _video;
        private Android.Media.MediaPlayer _player;
#if API_LEVEL_11_OR_HIGHER
        private const string _vertexShader =
                "uniform mat4 uMVPMatrix;\n" +
                "uniform mat4 uSTMatrix;\n" +
                "attribute vec4 aPosition;\n" +
                "attribute vec4 aTextureCoord;\n" +
                "varying vec2 vTextureCoord;\n" +
                "void main() {\n" +
                "  gl_Position = uMVPMatrix * aPosition;\n" +
                "  vTextureCoord = (uSTMatrix * aTextureCoord).xy;\n" +
                "}\n";

        private const string _fragmentShader =
                "#extension GL_OES_EGL_image_external : require\n" +
                "precision mediump float;\n" +
                "varying vec2 vTextureCoord;\n" +
                "uniform samplerExternalOES sTexture;\n" +
                "void main() {\n" +
                "  gl_FragColor = texture2D(sTexture, vTextureCoord);\n" +
                "}\n";

        private int shaderProgram;
        private int mvpMatrixHandle;
        private int stMatrixHandle;
        private int sTextureHandle;
        private int positionHandle;
        private int texCoordHandle;

        private float[] pos = new float[] {
            // X,   Y,    Z,
            -1.0f, -1.0f, 0,
             1.0f, -1.0f, 0,
            -1.0f,  1.0f, 0,
             1.0f,  1.0f, 0,
        };
        private float[] tex = new float[] {
            // U, V
            0.0f, 1.0f,
            1.0f, 1.0f,
            0.0f, 0.0f,
            1.0f, 0.0f,
        };
        private int videoSurfaceTexture;
        private int rgbaFramebuffer;

        // Used to restore our previous GL state.
        private int oldTexture;
        private int oldShader;
        private int oldFramebuffer;
        private int oldActiveTexture;
        private int[] oldViewport;
        private bool oldCullState;
        private bool oldDepthMask;
        private bool oldDepthTest;
        private bool oldBlendState;

        const TextureTarget textureTarget = (TextureTarget)0x8D65;

        private Texture2D _texture;
        private Android.Graphics.SurfaceTexture _surfaceTexture;
        private Object _textureUpdateLock = new Object();
        private bool _textureNeedsUpdate;
        private float[] _matrix = new float[16];
        private float[] _identity = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        private void GL_initialize()
        {
            // Initialize the old viewport array.
            oldViewport = new int[4];

            // Create the YUV textures.
            GL.GenTextures(1, ref videoSurfaceTexture);
            GraphicsExtensions.CheckGLError();

            // Create the RGBA framebuffer target.
            GL.GenFramebuffers(1, ref rgbaFramebuffer);
            GraphicsExtensions.CheckGLError();

            // Create the vertex/fragment shaders.
            int vshader_id = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vshader_id, 1, new string[] { _vertexShader }, (int[])null);
            GraphicsExtensions.CheckGLError();
            GL.CompileShader(vshader_id);
            GraphicsExtensions.CheckGLError();
            int fshader_id = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fshader_id, 1, new string[] { _fragmentShader }, (int[])null);
            GraphicsExtensions.CheckGLError();
            GL.CompileShader(fshader_id);
            GraphicsExtensions.CheckGLError();

            // Create the shader program.
            shaderProgram = GL.CreateProgram();
            GL.AttachShader(shaderProgram, vshader_id);
            GraphicsExtensions.CheckGLError();
            GL.AttachShader(shaderProgram, fshader_id);
            GraphicsExtensions.CheckGLError();
            GL.LinkProgram(shaderProgram);
            GraphicsExtensions.CheckGLError();
            GL.DeleteShader(vshader_id);
            GraphicsExtensions.CheckGLError();
            GL.DeleteShader(fshader_id);
            GraphicsExtensions.CheckGLError();

            mvpMatrixHandle = GL.GetUniformLocation(shaderProgram, "uMVPMatrix");
            GraphicsExtensions.CheckGLError();
            stMatrixHandle = GL.GetUniformLocation(shaderProgram, "uSTMatrix");
            GraphicsExtensions.CheckGLError();
            sTextureHandle = GL.GetUniformLocation(shaderProgram, "sTexture");
            GraphicsExtensions.CheckGLError();
            positionHandle = GL.GetAttribLocation(shaderProgram, "aPosition");
            GraphicsExtensions.CheckGLError();
            texCoordHandle = GL.GetAttribLocation(shaderProgram, "aTextureCoord");
            GraphicsExtensions.CheckGLError();
        }

        private void GL_dispose()
        {
            // Delete the shader program.
            GL.DeleteProgram(shaderProgram);
            GraphicsExtensions.CheckGLError();

            // Delete the RGBA framebuffer target.
            GL.DeleteFramebuffers(1, ref rgbaFramebuffer);
            GraphicsExtensions.CheckGLError();

            // Delete the YUV textures.
            GL.DeleteTextures(1, ref videoSurfaceTexture);
            GraphicsExtensions.CheckGLError();
        }

        private void GL_setupTargets(int width, int height)
        {
            // We're going to be messing with things to do this...
            GL_pushState();

            // Attach the Texture2D to the framebuffer.
            GL.BindFramebuffer((All)FramebufferTarget.Framebuffer, rgbaFramebuffer);
            GraphicsExtensions.CheckGLError();
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _texture.glTexture,
                0
            );
            GraphicsExtensions.CheckGLError();

            // We'll just use this for all the texture work.
            GL.ActiveTexture((All)TextureUnit.Texture0);
            GraphicsExtensions.CheckGLError();

            // Bind the desired texture.
            GL.BindTexture(textureTarget, videoSurfaceTexture);
            GraphicsExtensions.CheckGLError();

            // Set the texture parameters, for completion/consistency's sake.
            GL.TexParameter(textureTarget, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GraphicsExtensions.CheckGLError();
            GL.TexParameter(textureTarget, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GraphicsExtensions.CheckGLError();

            GL_popState();
        }

        private void GL_pushState()
        {
            GL.GetInteger(GetPName.Viewport, oldViewport);
            GraphicsExtensions.CheckGLError();
            GL.GetInteger(GetPName.CurrentProgram, ref oldShader);
            GraphicsExtensions.CheckGLError();
            GL.GetInteger(GetPName.ActiveTexture, ref oldActiveTexture);
            GraphicsExtensions.CheckGLError();
            GL.ActiveTexture(TextureUnit.Texture0);
            GraphicsExtensions.CheckGLError();
            GL.GetInteger(GetPName.TextureBinding2D, ref oldTexture);
            GraphicsExtensions.CheckGLError();
            GL.GetInteger(GetPName.FramebufferBinding, ref oldFramebuffer);
            GraphicsExtensions.CheckGLError();
            oldCullState = GL.IsEnabled(EnableCap.CullFace);
            GL.Disable(EnableCap.CullFace);
            GraphicsExtensions.CheckGLError();
            GL.GetBoolean(GetPName.DepthWritemask, ref oldDepthMask);
            GraphicsExtensions.CheckGLError();
            GL.DepthMask(false);
            GraphicsExtensions.CheckGLError();
            oldDepthTest = GL.IsEnabled(EnableCap.DepthTest);
            GL.Disable(EnableCap.DepthTest);
            GraphicsExtensions.CheckGLError();
            oldBlendState = GL.IsEnabled(EnableCap.Blend);
            GL.Disable(EnableCap.Blend);
            GraphicsExtensions.CheckGLError();
        }

        private void GL_popState()
        {
            GL.Viewport(oldViewport[0], oldViewport[1], oldViewport[2], oldViewport[3]);
            GraphicsExtensions.CheckGLError();
            GL.UseProgram(oldShader);
            GraphicsExtensions.CheckGLError();
            GL.ActiveTexture(TextureUnit.Texture0);
            GraphicsExtensions.CheckGLError();
            GL.BindTexture(TextureTarget.Texture2D, oldTexture);
            GraphicsExtensions.CheckGLError();
            GL.ActiveTexture((TextureUnit)oldActiveTexture);
            GraphicsExtensions.CheckGLError();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, oldFramebuffer);
            GraphicsExtensions.CheckGLError();
            if (oldCullState)
            {
                GL.Enable(EnableCap.CullFace);
                GraphicsExtensions.CheckGLError();
            }
            GL.DepthMask(oldDepthMask);
            GraphicsExtensions.CheckGLError();
            if (oldDepthTest)
            {
                GL.Enable(EnableCap.DepthTest);
                GraphicsExtensions.CheckGLError();
            }
            if (oldBlendState)
            {
                GL.Enable(EnableCap.Blend);
                GraphicsExtensions.CheckGLError();
            }
        }
#endif
        private MediaState _state;
		private bool _isLooped;
        private bool _isMuted;
        private float _volume;
        private bool _isDisposed;

        /// <summary>
        /// Creates a new instance the VideoPlayer object.
        /// </summary>
        public VideoPlayer()
        {
			_state = MediaState.Stopped;
            _volume = 1.0f;
        }

        ~VideoPlayer()
        {
            Dispose(false);
        }

#if API_LEVEL_11_OR_HIGHER
        void UpdateTexture()
        {
            _surfaceTexture.UpdateTexImage();
            _surfaceTexture.GetTransformMatrix(_matrix);

            // Set up an environment to muck about in.
            GL_pushState();

            // Bind our shader program.
            GL.UseProgram(shaderProgram);
            GraphicsExtensions.CheckGLError();

            // Set uniform values.
            GL.Uniform1(sTextureHandle, 0);
            GraphicsExtensions.CheckGLError();

            GL.UniformMatrix4(mvpMatrixHandle, 1, false, _identity);
            GraphicsExtensions.CheckGLError();
            GL.UniformMatrix4(stMatrixHandle, 1, false, _matrix);
            GraphicsExtensions.CheckGLError();

            // Set up the vertex pointers/arrays.
            GL.VertexAttribPointer(positionHandle, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), pos);
            GraphicsExtensions.CheckGLError();
            GL.VertexAttribPointer(texCoordHandle, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), tex);
            GraphicsExtensions.CheckGLError();
            GL.EnableVertexAttribArray(positionHandle);
            GraphicsExtensions.CheckGLError();
            GL.EnableVertexAttribArray(texCoordHandle);
            GraphicsExtensions.CheckGLError();

            // Bind our target framebuffer.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, rgbaFramebuffer);
            GraphicsExtensions.CheckGLError();

            // Prepare GL textures with our current frame data
            GL.ActiveTexture(TextureUnit.Texture0);
            GraphicsExtensions.CheckGLError();
            GL.BindTexture(textureTarget, videoSurfaceTexture);
            GraphicsExtensions.CheckGLError();

            // Flip the viewport, because loldirectx
            GL.Viewport(0, 0, _texture.Width, _texture.Height);
            GraphicsExtensions.CheckGLError();

            // Draw the YUV textures to the framebuffer with our shader.
            GL.DrawArrays(BeginMode.TriangleStrip, 0, 4);
            GraphicsExtensions.CheckGLError();

            // Clean up after ourselves.
            GL_popState();
        }

        public Texture2D GetTexture()
        {
            lock (_textureUpdateLock)
            {
                if (_textureNeedsUpdate)
                {
                    UpdateTexture();
                    _textureNeedsUpdate = false;
                }
            }
            return _texture;
        }
#endif

        void Prepare()
        {
            _player = new Android.Media.MediaPlayer();
            if (_player != null)
            {
#if API_LEVEL_11_OR_HIGHER
                GL_initialize();
                _surfaceTexture = new Android.Graphics.SurfaceTexture(videoSurfaceTexture);
                _surfaceTexture.FrameAvailable += new EventHandler<Android.Graphics.SurfaceTexture.FrameAvailableEventArgs>(SurfaceTexture_FrameAvailable);
                _player.SetSurface(new Android.Views.Surface(_surfaceTexture));
#else
                _player.SetDisplay(Game.Instance.Window.Holder);
#endif
                var afd = Game.Activity.Assets.OpenFd(_video.FileName);
                if (afd != null)
                {
                    _player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                    _player.Prepare();
#if API_LEVEL_11_OR_HIGHER
                    _texture = new Texture2D(Game.Instance.GraphicsDevice, _player.VideoWidth, _player.VideoHeight);
                    GL_setupTargets(_player.VideoWidth, _player.VideoHeight);
                    _textureNeedsUpdate = true;
#endif
                    _video.Duration = TimeSpan.FromMilliseconds(_player.Duration);
                    _video.Width = _player.VideoWidth;
                    _video.Height = _player.VideoHeight;
                }
            }
        }

#if API_LEVEL_11_OR_HIGHER
        void SurfaceTexture_FrameAvailable(object sender, Android.Graphics.SurfaceTexture.FrameAvailableEventArgs e)
        {
            lock (_textureUpdateLock)
            {
                _textureNeedsUpdate = true;
            }
        }
#endif

        /// <summary>
        /// Pauses the currently playing video.
        /// </summary>
        public void Pause()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("VideoPlayer");
            if (_state != MediaState.Playing)
                throw new InvalidOperationException("Video not playing");
            if (_player != null)
            {
                _player.Pause();
                _state = MediaState.Paused;
            }
        }

        /// <summary>
        /// Resumes the paused video.
        /// </summary>
        public void Resume()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("VideoPlayer");
            if (_state != MediaState.Paused)
                throw new InvalidOperationException("Video not paused");
            if (_player != null)
            {
                _player.Start();
                _state = MediaState.Playing;
            }
        }

        /// <summary>
        /// Gets the current state of the video playback.
        /// </summary>
		public MediaState State
        {
            get
            {
				return _state;
            }
        }

        /// <summary>
        /// Plays the given video.
        /// </summary>
        /// <param name="video">The video to play.</param>
        public void Play(Microsoft.Xna.Framework.Media.Video video)
        {	
            if (_isDisposed)
                throw new ObjectDisposedException("VideoPlayer");

            _video = video;
            Prepare();
            if (_player != null)
            {
                _player.Looping = _isLooped;
                _player.Start();
                _player.SetVolume(_volume, _volume);

                _state = MediaState.Playing;
            }
        }

        /// <summary>
        /// Stops the current video.
        /// </summary>
        public void Stop()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("VideoPlayer");

            if (_player != null)
            {
                _player.Stop();
                _player.SetDisplay(null);
                _player.Dispose();
                _player = null;
#if API_LEVEL_11_OR_HIGHER
                if (_surfaceTexture != null)
                {
                    _surfaceTexture.FrameAvailable -= new EventHandler<Android.Graphics.SurfaceTexture.FrameAvailableEventArgs>(SurfaceTexture_FrameAvailable);
                    _surfaceTexture.Dispose();
                    _surfaceTexture = null;
                }
                if (_texture != null)
                {
                    _texture.Dispose();
                    _texture = null;
                }
                GL_dispose();
#endif
                _state = MediaState.Stopped;
                _video = null;
            }
        }

        /// <summary>
        /// Gets or sets the looping state of the video.
        /// </summary>
        public bool IsLooped
        {
            get
            {
				return _isLooped;
            }
            set
            {
				_isLooped = value;
                if (_player != null)
                {
                    _player.Looping = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the muted state of the video.
        /// </summary>
        public bool IsMuted
        {
            get
            {
                return _isMuted;
            }
            set
            {
                _isMuted = value;
                if (_player != null)
                {
                    _player.SetVolume(value ? 0.0f : _volume, value ? 0.0f : _volume);
                }
            }
        }

        /// <summary>
        /// Gets the video that is currently playing.
        /// </summary>
        public Microsoft.Xna.Framework.Media.Video Video
        {
            get
            {
                return _video;
            }
        }

        /// <summary>
        /// Gets or sets the video player volume.  Ranges from 0.0 (silent) to 1.0 (full).
        /// </summary>
        public float Volume
        {
            get
            {
                return _volume;
            }
            set
            {
                _volume = MathHelper.Clamp(value, 0.0f, 1.0f);
                if (_player != null && !_isMuted)
                    _player.SetVolume(_volume, _volume);
            }
        }

        /// <summary>
        /// Immediately releases unmanaged resources owned by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    if (_player != null)
                    {
                        if (_state != MediaState.Stopped)
                        {
                            _player.Stop();
                            _player.SetDisplay(null);
                        }
                        _player.Dispose();
                        _player = null;
#if API_LEVEL_11_OR_HIGHER
                        if (_surfaceTexture != null)
                        {
                            _surfaceTexture.FrameAvailable -= new EventHandler<Android.Graphics.SurfaceTexture.FrameAvailableEventArgs>(SurfaceTexture_FrameAvailable);
                            _surfaceTexture.Dispose();
                            _surfaceTexture = null;
                        }
                        if (_texture != null)
                        {
                            _texture.Dispose();
                            _texture = null;
                        }
#endif
                    }
                }
                GL_dispose();
                _video = null;
            }
            _isDisposed = true;
        }

        /// <summary>
        /// Gets the disposed state of the object.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                return _isDisposed;
            }
        }
	}
}
