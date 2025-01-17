﻿// Crest Ocean System

// Copyright 2020 Wave Harmonic Ltd

using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crest
{
    /// <summary>
    /// Renders terrain height / ocean depth once into a render target to cache this off and avoid rendering it every frame.
    /// This should be used for static geometry, dynamic objects should be tagged with the Render Ocean Depth component.
    /// </summary>
    [ExecuteAlways]
    [HelpURL("https://crest.readthedocs.io/en/latest/user/shallows-and-shorelines.html")]
    [AddComponentMenu(Internal.Constants.MENU_PREFIX_SCRIPTS + "Ocean Depth Cache")]
    public partial class OceanDepthCache : MonoBehaviour
    {
        public enum OceanDepthCacheType
        {
            Realtime,
            Baked,
        }

        public enum OceanDepthCacheRefreshMode
        {
            OnStart,
            OnDemand,
        }

        [Tooltip("Realtime = cache will be dynamic in accordance to refresh mode, Baked = cache will use the provided texture."), SerializeField]
        OceanDepthCacheType _type = OceanDepthCacheType.Realtime;
        public OceanDepthCacheType Type => _type;

        [Tooltip("Ignored if baked. On Start = cache will populate in Start(), On Demand = call PopulateCache() manually via scripting."), SerializeField]
        OceanDepthCacheRefreshMode _refreshMode = OceanDepthCacheRefreshMode.OnStart;
        public OceanDepthCacheRefreshMode RefreshMode => _refreshMode;

        [Tooltip("The layers to render into the depth cache.")]
        public LayerMask _layers = 1; // Default

        [Obsolete("Layer Names (string[] _layerNames) is obsolete and is no longer used. Use Layers (LayerMask _layers) instead."), HideInInspector]
        public string[] _layerNames = new string[0];

        [Tooltip("The resolution of the cached depth - lower will be more efficient.")]
        public int _resolution = 512;

        // A big hill will still want to write its height into the depth texture
        [Tooltip("The 'near plane' for the depth cache camera (top down)."), SerializeField]
        float _cameraMaxTerrainHeight = 100f;

        [Tooltip("Will render into the cache every frame. Intended for debugging, will generate garbage."), SerializeField]
#pragma warning disable 414
        bool _forceAlwaysUpdateDebug = false;
#pragma warning restore 414

        [Tooltip("Hides the depth cache camera, for cleanliness. Disable to make it visible in the Hierarchy."), SerializeField]
        bool _hideDepthCacheCam = true;

        [Tooltip("Baked depth cache. Baking button available in play mode."), SerializeField]
#pragma warning disable 649
        Texture2D _savedCache;
#pragma warning restore 649
        public Texture2D SavedCache => _savedCache;

#pragma warning disable 414
        [Tooltip("Editor only: run validation checks on Start() to check for issues."), SerializeField]
        bool _runValidationOnStart = true;
#pragma warning restore 414

        RenderTexture _cacheTexture;
        public RenderTexture CacheTexture => _cacheTexture;

        GameObject _drawCacheQuad;
        Camera _camDepthCache;
        Material _copyDepthMaterial;

        void Start()
        {
            if (OceanRenderer.Instance == null)
            {
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            if (EditorApplication.isPlaying && _runValidationOnStart)
            {
                Validate(OceanRenderer.Instance, ValidatedHelper.DebugLog);
            }
#endif

            if (_type == OceanDepthCacheType.Baked)
            {
                InitCacheQuad();
            }
            else if (_type == OceanDepthCacheType.Realtime && _refreshMode == OceanDepthCacheRefreshMode.OnStart)
            {
                PopulateCache();
            }
        }

#if UNITY_EDITOR
        void Update()
        {
            // We need to switch the quad texture if the user changes the cache type in the editor.
            InitCacheQuad();

            if (_forceAlwaysUpdateDebug)
            {
                PopulateCache(updateComponents: true);
            }
        }
#endif

        float CalculateCacheCameraOrthographicSize()
        {
            return Mathf.Max(transform.lossyScale.x / 2f, transform.lossyScale.z / 2f);
        }

        Vector3 CalculateCacheCameraPosition()
        {
            return transform.position + Vector3.up * _cameraMaxTerrainHeight;
        }

        bool IsCacheTextureOutdated(RenderTexture texture)
        {
            return texture != null && (texture.width != _resolution || texture.height != _resolution);
        }

        RenderTexture MakeRT(bool depthStencilTarget)
        {
            RenderTextureFormat fmt;

            if (depthStencilTarget)
            {
                fmt = RenderTextureFormat.Depth;
            }
            else
            {
#if UNITY_EDITOR_WIN
                fmt = RenderTextureFormat.DefaultHDR;
#else
                fmt = RenderTextureFormat.RHalf;
#endif
            }

            Debug.Assert(SystemInfo.SupportsRenderTextureFormat(fmt), "The graphics device does not support the render texture format " + fmt.ToString());
            var result = new RenderTexture(_resolution, _resolution, depthStencilTarget ? 24 : 0);
            result.name = gameObject.name + "_oceanDepth_" + (depthStencilTarget ? "DepthOnly" : "Cache");
            result.format = fmt;
            result.useMipMap = false;
            result.anisoLevel = 0;
            return result;
        }

        bool InitObjects(bool updateComponents)
        {
            if (updateComponents && IsCacheTextureOutdated(_cacheTexture))
            {
                // Destroy the texture so it can be recreated.
                _cacheTexture.Release();
                _cacheTexture = null;
            }

            if (_cacheTexture == null)
            {
                _cacheTexture = MakeRT(false);
            }

            // We want to know this later.
            var isDepthCacheCameraCreation = _camDepthCache == null;

            if (_layers == 0)
            {
                Debug.LogError("No valid layers for populating depth cache, aborting.", this);
                return false;
            }

            if (isDepthCacheCameraCreation)
            {
                _camDepthCache = new GameObject("DepthCacheCam").AddComponent<Camera>();
                _camDepthCache.transform.parent = transform;
                _camDepthCache.transform.localEulerAngles = 90f * Vector3.right;
                _camDepthCache.orthographic = true;
                _camDepthCache.clearFlags = CameraClearFlags.SolidColor;
                // Clear to 'very deep'
                _camDepthCache.backgroundColor = Color.white * 1000f;
                _camDepthCache.enabled = false;
                _camDepthCache.allowMSAA = false;
                _camDepthCache.allowDynamicResolution = false;
                
                // Stops behaviour from changing in VR. I tried disabling XR before/after camera render but it makes the editor
                // go bonkers with split windows.
                _camDepthCache.cameraType = CameraType.Reflection;
                // I'd prefer to destroy the camera object, but I found sometimes (on first start of editor) it will fail to render.
                _camDepthCache.gameObject.SetActive(false);

                var additionalCameraData = _camDepthCache.gameObject.AddComponent<UniversalAdditionalCameraData>();
                additionalCameraData.renderShadows = false;
                additionalCameraData.requiresColorTexture = false;
                additionalCameraData.requiresDepthTexture = false;
                additionalCameraData.renderPostProcessing = false;
            }

            if (updateComponents || isDepthCacheCameraCreation)
            {
                // Calculate here so it is always updated.
                _camDepthCache.transform.position = CalculateCacheCameraPosition();
                _camDepthCache.orthographicSize = CalculateCacheCameraOrthographicSize();
                _camDepthCache.cullingMask = _layers;
                _camDepthCache.gameObject.hideFlags = _hideDepthCacheCam ? HideFlags.HideAndDontSave : HideFlags.DontSave;
            }

            if (updateComponents && IsCacheTextureOutdated(_camDepthCache.targetTexture))
            {
                // Destroy the texture so it can be recreated.
                _camDepthCache.targetTexture.Release();
                _camDepthCache.targetTexture = null;
            }

            if (_camDepthCache.targetTexture == null)
            {
                _camDepthCache.targetTexture = MakeRT(true);
            }

            InitCacheQuad();

            return true;
        }

        void InitCacheQuad()
        {
            Renderer qr;

            if (_drawCacheQuad == null)
            {
                _drawCacheQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _drawCacheQuad.hideFlags = HideFlags.DontSave;
#if UNITY_EDITOR
                DestroyImmediate(_drawCacheQuad.GetComponent<Collider>());
#else
                Destroy(_drawCacheQuad.GetComponent<Collider>());
#endif
                _drawCacheQuad.name = "DepthCache_" + gameObject.name + "_NOSAVE";
                _drawCacheQuad.transform.SetParent(transform, false);
                _drawCacheQuad.transform.localEulerAngles = 90f * Vector3.right;
                _drawCacheQuad.AddComponent<RegisterSeaFloorDepthInput>()._assignOceanDepthMaterial = false;
                qr = _drawCacheQuad.GetComponent<Renderer>();
                qr.sharedMaterial = new Material(Shader.Find(LodDataMgrSeaFloorDepth.ShaderName));
            }
            else
            {
                qr = _drawCacheQuad.GetComponent<Renderer>();
            }

            if (_type == OceanDepthCacheType.Baked)
            {
                qr.sharedMaterial.mainTexture = _savedCache;
            }
            else
            {
                qr.sharedMaterial.mainTexture = _cacheTexture;
            }

            qr.enabled = false;
        }

        /// <summary>
        /// Populates the ocean depth cache. Call this method if using <i>On Demand<i>.
        /// </summary>
        /// <param name="updateComponents">
        /// Updates components like the depth cache camera. Pass true if you have changed any depth cache properties.
        /// </param>
        public void PopulateCache(bool updateComponents = false)
        {
            // Nothing to populate for baked.
            if (_type == OceanDepthCacheType.Baked)
            {
                return;
            }

            // Make sure we have required objects.
            if (!InitObjects(updateComponents))
            {
                return;
            }

            // Render scene, saving depths in depth buffer.
            _camDepthCache.Render();

            if (_copyDepthMaterial == null)
            {
                _copyDepthMaterial = new Material(Shader.Find("Crest/Copy Depth Buffer Into Cache"));
            }

            // Shader needs sea level to determine water depth. Ocean instance might not be available in prefabs.
            var centerPoint = Vector3.zero;
            centerPoint.y = OceanRenderer.Instance != null
                ? OceanRenderer.Instance.Root.position.y : transform.position.y;

            _copyDepthMaterial.SetVector("_OceanCenterPosWorld", centerPoint);

            _copyDepthMaterial.SetTexture("_CamDepthBuffer", _camDepthCache.targetTexture);

            // Zbuffer params
            //float4 _ZBufferParams;            // x: 1-far/near,     y: far/near, z: x/far,     w: y/far
            float near = _camDepthCache.nearClipPlane, far = _camDepthCache.farClipPlane;
            _copyDepthMaterial.SetVector("_CustomZBufferParams", new Vector4(1f - far / near, far / near, (1f - far / near) / far, (far / near) / far));

            // Altitudes for near and far planes
            float ymax = _camDepthCache.transform.position.y - near;
            float ymin = ymax - far;
            _copyDepthMaterial.SetVector("_HeightNearHeightFar", new Vector2(ymax, ymin));

            // Copy from depth buffer into the cache
            Graphics.Blit(null, _cacheTexture, _copyDepthMaterial);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0f, 1f));

            if (_type == OceanDepthCacheType.Realtime)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
                Gizmos.DrawCube(Vector3.up * _cameraMaxTerrainHeight / transform.lossyScale.y, new Vector3(1f, 0f, 1f));
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(OceanDepthCache))]
    public class OceanDepthCacheEditor : ValidatedEditor
    {
        readonly string[] _propertiesToExclude = new string[] { "m_Script", "_type", "_refreshMode", "_savedCache", "_layers", "_resolution", "_cameraMaxTerrainHeight", "_forceAlwaysUpdateDebug" };

        public override void OnInspectorGUI()
        {
            // We won't just use default inspector because we want to show some of the params conditionally based on cache type

            // First show standard 'Script' field
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = true;

            // Next expose cache type and refresh mode

            var typeProp = serializedObject.FindProperty("_type");
            EditorGUILayout.PropertyField(typeProp);

            var cacheType = (OceanDepthCache.OceanDepthCacheType)typeProp.intValue;

            if (cacheType == OceanDepthCache.OceanDepthCacheType.Realtime)
            {
                // Only expose the following if real-time cache type
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_refreshMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_layers"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_resolution"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_cameraMaxTerrainHeight"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_forceAlwaysUpdateDebug"));
            }
            else
            {
                // Only expose saved cache if non-real-time
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_savedCache"));
            }

            // Draw rest of inspector fields
            DrawPropertiesExcluding(serializedObject, _propertiesToExclude);

            // Apply inspector changes
            serializedObject.ApplyModifiedProperties();

            var playing = EditorApplication.isPlaying;

            var dc = target as OceanDepthCache;
            var isOnDemand = cacheType == OceanDepthCache.OceanDepthCacheType.Realtime &&
                dc.RefreshMode == OceanDepthCache.OceanDepthCacheRefreshMode.OnDemand;
            var isBakeable = cacheType == OceanDepthCache.OceanDepthCacheType.Realtime &&
                (!isOnDemand || dc.CacheTexture != null);

            if ((!playing || isOnDemand) && dc.Type != OceanDepthCache.OceanDepthCacheType.Baked && GUILayout.Button("Populate cache"))
            {
                dc.PopulateCache(updateComponents: true);
            }

            if (isBakeable && GUILayout.Button("Save cache to file"))
            {
                var rt = dc.CacheTexture;
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;

                byte[] bytes;
                bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

                string path = dc.SavedCache ?
                    AssetDatabase.GetAssetPath(dc.SavedCache) : $"Assets/OceanDepthCache_{Guid.NewGuid()}.exr";
                System.IO.File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);

                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                ti.textureType = TextureImporterType.SingleChannel;
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.alphaIsTransparency = false;
                ti.SaveAndReimport();

                Debug.Log("Cache saved to " + path, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path));
            }

            ShowValidationMessages();
        }
    }

    public partial class OceanDepthCache : IValidated
    {
        bool IsCacheOutdated()
        {
            return _camDepthCache.orthographicSize != CalculateCacheCameraOrthographicSize() ||
                _camDepthCache.transform.position != CalculateCacheCameraPosition() ||
                IsCacheTextureOutdated(_camDepthCache.targetTexture) ||
                IsCacheTextureOutdated(_cacheTexture);
        }

        public bool Validate(OceanRenderer ocean, ValidatedHelper.ShowMessage showMessage)
        {
            var isValid = true;

            isValid = ValidateObsolete(ocean, showMessage);

            if (_camDepthCache != null && _camDepthCache.targetTexture != null && _cacheTexture != null)
            {
                if (IsCacheOutdated())
                {
                    showMessage
                    (
                        "Depth cache is outdated. Click <i>Populate Cache</i> or re-bake the cache to bring the cache " +
                        "up-to-date with component changes.",
                        ValidatedHelper.MessageType.Warning, this
                    );
                }
            }

            if (_type == OceanDepthCacheType.Baked)
            {
                if (_savedCache == null)
                {
                    showMessage
                    (
                        "Depth cache type is 'Saved Cache' but no saved cache data is provided.",
                        ValidatedHelper.MessageType.Error, this
                    );

                    isValid = false;
                }
            }
            else
            {
                if (_layers == 0)
                {
                    showMessage
                    (
                        "No layers specified for rendering into depth cache.",
                        ValidatedHelper.MessageType.Error, this
                    );

                    isValid = false;
                }

                if (_forceAlwaysUpdateDebug)
                {
                    showMessage
                    (
                        $"<i>Force Always Update Debug</i> option is enabled on depth cache <i>{gameObject.name}</i>, which means it will render every frame instead of running from the cache.",
                        ValidatedHelper.MessageType.Warning, this
                    );

                    isValid = false;
                }

                if (_resolution < 4)
                {
                    showMessage
                    (
                        $"Cache resolution {_resolution} is very low. Is this intentional?",
                        ValidatedHelper.MessageType.Error, this
                    );

                    isValid = false;
                }

                if (!Mathf.Approximately(transform.lossyScale.x, transform.lossyScale.z))
                {
                    showMessage
                    (
                        "The <i>Ocean Depth Cache</i> in realtime only supports a uniform scale for X and Z. " +
                        "These values currently do not match. " +
                        $"Its current scale in the hierarchy is: X = {transform.lossyScale.x} Z = {transform.lossyScale.z}.",
                        ValidatedHelper.MessageType.Error, this
                    );

                    isValid = false;
                }

                // We used to test if nothing is present that would render into the cache, but these could probably come from other scenes.
            }

            if (transform.lossyScale.magnitude < 5f)
            {
                showMessage
                (
                    "Ocean depth cache transform scale is small and will capture a small area of the world. The scale sets the size of the area that will be cached, and this cache is set to render a very small area.",
                    ValidatedHelper.MessageType.Warning, this
                );

                isValid = false;
            }

            if (transform.lossyScale.y < 0.001f || transform.localScale.y < 0.01f)
            {
                showMessage
                (
                    $"Ocean depth cache scale Y should be set to 1.0. Its current scale in the hierarchy is {transform.lossyScale.y}.",
                    ValidatedHelper.MessageType.Error, this
                );

                isValid = false;
            }

            if (ocean != null && ocean.Root != null && Mathf.Abs(transform.position.y - ocean.Root.position.y) > 0.00001f)
            {
                showMessage
                (
                    "It is recommended that the cache is placed at the same height (y component of position) as the ocean, i.e. at the sea level. If the cache is created before the ocean is present, the cache height will inform the sea level.",
                    ValidatedHelper.MessageType.Warning, this
                );

                isValid = false;
            }

            // Check that there are no renderers in descendants.
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > (Application.isPlaying ? 1 : 0))
            {
                Renderer quadRenderer = _drawCacheQuad ? _drawCacheQuad.GetComponent<Renderer>() : null;

                foreach (var renderer in renderers)
                {
                    if (ReferenceEquals(renderer, quadRenderer)) continue;

                    showMessage
                    (
                        "It is not expected that a depth cache object has a Renderer component in its hierarchy." +
                        "The cache is typically attached to an empty GameObject. Please refer to the example content.",
                        ValidatedHelper.MessageType.Warning, renderer
                    );

                    // Reporting only one renderer at a time will be enough to avoid overwhelming user and UI.
                    break;
                }

                isValid = false;
            }

            return isValid;
        }

#pragma warning disable 0618
        public bool ValidateObsolete(OceanRenderer ocean, ValidatedHelper.ShowMessage showMessage)
        {
            var isValid = true;

            if (_layerNames?.Length > 0)
            {
                showMessage
                (
                    "<i>Layer Names</i> on the <i>Ocean Depth Cache</i> is obsolete and is no longer used. " +
                    "Use <i>Layers</i> instead.",
                    ValidatedHelper.MessageType.Error, this, (SerializedObject serializedObject) =>
                    {
                        serializedObject.FindProperty("_layers").intValue = LayerMask.GetMask(_layerNames);
                        serializedObject.FindProperty("_layerNames").arraySize = 0;
                    }
                );

                isValid = false;
            }

            return isValid;
        }
#pragma warning restore 0618
    }
#endif
}
