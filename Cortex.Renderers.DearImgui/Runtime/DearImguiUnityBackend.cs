using System;
using Cortex.Core.Diagnostics;
using Cortex.Presentation.Runtime;
using Cortex.Renderers.DearImgui.Native;
using UnityEngine;

namespace Cortex.Renderers.DearImgui
{
    internal sealed unsafe class DearImguiUnityBackend : IDisposable
    {
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.DearImgui");

        private struct RenderDrawStats
        {
            public bool HadValidDrawData;
            public bool MaterialPassApplied;
            public int DrawListCount;
            public int CommandCount;
            public int SubmittedCommandCount;
            public uint SubmittedElementCount;
            public int EmptyCommandSkipCount;
            public int CallbackSkipCount;
            public int TextureSkipCount;
            public string ShaderName;
        }

        private struct DrawVertex
        {
            public float X;
            public float Y;
            public float U;
            public float V;
            public Color32 Color;
        }

        private static readonly IntPtr FontTextureId = new IntPtr(1);
        private readonly DearImguiShellPresenter _presenter = new DearImguiShellPresenter();
        private IntPtr _context;
        private Texture2D _fontTexture;
        private Material _material;
        private bool _initialized;
        private int _lastRenderedFrame = -1;
        private RenderDrawStats _lastDrawStats;
        private bool _loggedFirstVisibleRenderDiagnostics;

        public bool Render(CortexShellController controller)
        {
            if (controller == null)
            {
                return false;
            }

            if (_lastRenderedFrame == Time.frameCount)
            {
                return false;
            }

            try
            {
                EnsureInitialized();
                var io = DearImguiNative.igGetIO();
                if (io == null)
                {
                    return false;
                }

                io->DisplaySize = new DearImguiNative.ImVec2(Screen.width, Screen.height);
                io->DisplayFramebufferScale = new DearImguiNative.ImVec2(1f, 1f);
                io->DeltaTime = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : (1f / 60f);
                DearImguiInputAdapter.Update(io);
                DearImguiNative.igNewFrame();
                var presentedVisibleShell = _presenter.Draw(controller, Screen.width, Screen.height);
                if (!presentedVisibleShell)
                {
                    return false;
                }

                DearImguiNative.igRender();
                _lastDrawStats = RenderDrawData(DearImguiNative.igGetDrawData());
                _lastRenderedFrame = Time.frameCount;
                if (!_loggedFirstVisibleRenderDiagnostics)
                {
                    _loggedFirstVisibleRenderDiagnostics = true;
                    Log.WriteInfo("Dear ImGui backend submitted first visible shell frame. " + DescribeLastRenderStats());
                }

                if (_lastDrawStats.SubmittedCommandCount <= 0 || _lastDrawStats.SubmittedElementCount <= 0u)
                {
                    Log.WriteWarning("Dear ImGui visible shell produced no submitted geometry. " + DescribeLastRenderStats());
                }

                return true;
            }
            catch (DllNotFoundException ex)
            {
                Log.WriteError("Native runtime unavailable. LoaderState=" + DearImguiNativeLoader.DescribeState() + " Exception=" + ex);
                controller.FallbackDearImguiToImguiFromRenderer("native-runtime-unavailable", "LoaderState=" + DearImguiNativeLoader.DescribeState() + " Exception=" + ex.Message);
                return false;
            }
            catch (BadImageFormatException ex)
            {
                Log.WriteError("Native runtime architecture mismatch. " + ex);
                controller.FallbackDearImguiToImguiFromRenderer("native-architecture-mismatch", ex.Message);
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                Log.WriteError("Managed/native export mismatch. " + ex);
                controller.FallbackDearImguiToImguiFromRenderer("managed-native-export-mismatch", ex.Message);
                return false;
            }
            catch (System.Runtime.InteropServices.MarshalDirectiveException ex)
            {
                Log.WriteError("Managed/native marshalling mismatch. " + ex);
                controller.FallbackDearImguiToImguiFromRenderer("managed-native-marshalling-mismatch", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                Log.WriteError("Render failed. " + ex);
                controller.FallbackDearImguiToImguiFromRenderer("dearimgui-render-failure", ex.ToString());
                return false;
            }
        }

        public void Dispose()
        {
            if (_fontTexture != null)
            {
                UnityEngine.Object.Destroy(_fontTexture);
                _fontTexture = null;
            }

            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
                _material = null;
            }

            if (_context != IntPtr.Zero)
            {
                DearImguiNative.igDestroyContext(_context);
                _context = IntPtr.Zero;
            }

            _initialized = false;
        }

        public string DescribeLastRenderStats()
        {
            return
                "DrawDataValid=" + _lastDrawStats.HadValidDrawData +
                ", DrawLists=" + _lastDrawStats.DrawListCount +
                ", Commands=" + _lastDrawStats.CommandCount +
                ", SubmittedCommands=" + _lastDrawStats.SubmittedCommandCount +
                ", SubmittedElements=" + _lastDrawStats.SubmittedElementCount +
                ", EmptySkips=" + _lastDrawStats.EmptyCommandSkipCount +
                ", CallbackSkips=" + _lastDrawStats.CallbackSkipCount +
                ", TextureSkips=" + _lastDrawStats.TextureSkipCount +
                ", MaterialPassApplied=" + _lastDrawStats.MaterialPassApplied +
                ", Shader=" + (_lastDrawStats.ShaderName ?? string.Empty) + ".";
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            DearImguiNativeLoader.EnsureLoaded();
            _context = DearImguiNative.igCreateContext(IntPtr.Zero);
            if (_context == IntPtr.Zero)
            {
                throw new InvalidOperationException("Dear ImGui context creation failed.");
            }

            var io = DearImguiNative.igGetIO();
            if (io == null)
            {
                throw new InvalidOperationException("Dear ImGui IO access failed.");
            }

            io->IniFilename = IntPtr.Zero;
            io->LogFilename = IntPtr.Zero;
            io->BackendFlags |= (int)DearImguiNative.ImGuiBackendFlags.RendererHasVtxOffset;
            CreateFontTexture(io->Fonts);
            _material = CreateMaterial();
            _initialized = true;
        }

        private void CreateFontTexture(IntPtr fontAtlas)
        {
            IntPtr pixels;
            int width;
            int height;
            int bytesPerPixel;
            DearImguiNative.ImFontAtlas_GetTexDataAsRGBA32(fontAtlas, out pixels, out width, out height, out bytesPerPixel);
            if (pixels == IntPtr.Zero || width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("Dear ImGui font atlas creation failed.");
            }

            var raw = new byte[width * height * bytesPerPixel];
            System.Runtime.InteropServices.Marshal.Copy(pixels, raw, 0, raw.Length);
            var colors = new Color32[width * height];
            for (var i = 0; i < colors.Length; i++)
            {
                var offset = i * bytesPerPixel;
                colors[i] = new Color32(raw[offset], raw[offset + 1], raw[offset + 2], raw[offset + 3]);
            }

            _fontTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            _fontTexture.name = "Cortex.DearImgui.FontAtlas";
            _fontTexture.hideFlags = HideFlags.HideAndDontSave;
            _fontTexture.wrapMode = TextureWrapMode.Clamp;
            _fontTexture.filterMode = FilterMode.Bilinear;
            _fontTexture.SetPixels32(colors);
            _fontTexture.Apply(false, false);
            DearImguiNative.ImFontAtlas_SetTexID(fontAtlas, FontTextureId);
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("GUI/Text Shader") ??
                Shader.Find("Hidden/Internal-GUITextureClip") ??
                Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                throw new InvalidOperationException("No compatible Unity shader was found for Dear ImGui rendering.");
            }

            var material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            material.mainTexture = _fontTexture;
            return material;
        }

        private RenderDrawStats RenderDrawData(DearImguiNative.ImDrawData* drawData)
        {
            var stats = new RenderDrawStats
            {
                HadValidDrawData = drawData != null && drawData->Valid,
                DrawListCount = drawData != null ? drawData->CmdListsCount : 0,
                ShaderName = _material != null && _material.shader != null ? _material.shader.name : string.Empty
            };
            if (drawData == null || !drawData->Valid || drawData->CmdListsCount <= 0 || _material == null)
            {
                return stats;
            }

            _material.mainTexture = _fontTexture;
            stats.MaterialPassApplied = _material.SetPass(0);

            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);

            var drawLists = (IntPtr*)drawData->CmdLists;
            for (var listIndex = 0; listIndex < drawData->CmdListsCount; listIndex++)
            {
                var drawList = (DearImguiNative.ImDrawList*)drawLists[listIndex];
                if (drawList == null)
                {
                    continue;
                }

                var vertices = (DearImguiNative.ImDrawVert*)drawList->VtxBuffer.Data;
                var indices = (ushort*)drawList->IdxBuffer.Data;
                var commands = (DearImguiNative.ImDrawCmd*)drawList->CmdBuffer.Data;
                if (vertices == null || indices == null || commands == null)
                {
                    continue;
                }

                for (var commandIndex = 0; commandIndex < drawList->CmdBuffer.Size; commandIndex++)
                {
                    stats.CommandCount++;
                    var command = commands[commandIndex];
                    if (command.UserCallback != IntPtr.Zero || command.ElemCount == 0)
                    {
                        if (command.UserCallback != IntPtr.Zero)
                        {
                            stats.CallbackSkipCount++;
                        }
                        else
                        {
                            stats.EmptyCommandSkipCount++;
                        }

                        continue;
                    }

                    if (command.TextureId != IntPtr.Zero && command.TextureId != FontTextureId)
                    {
                        stats.TextureSkipCount++;
                        continue;
                    }

                    stats.SubmittedCommandCount++;
                    stats.SubmittedElementCount += command.ElemCount;
                    var clipLeft = command.ClipRect.X - drawData->DisplayPos.X;
                    var clipTop = command.ClipRect.Y - drawData->DisplayPos.Y;
                    var clipRight = command.ClipRect.Z - drawData->DisplayPos.X;
                    var clipBottom = command.ClipRect.W - drawData->DisplayPos.Y;

                    GL.Begin(GL.TRIANGLES);
                    for (uint elemIndex = 0; elemIndex + 2u < command.ElemCount; elemIndex += 3u)
                    {
                        var firstVertex = BuildVertex(vertices[indices[command.IdxOffset + elemIndex] + command.VtxOffset], drawData);
                        var secondVertex = BuildVertex(vertices[indices[command.IdxOffset + elemIndex + 1u] + command.VtxOffset], drawData);
                        var thirdVertex = BuildVertex(vertices[indices[command.IdxOffset + elemIndex + 2u] + command.VtxOffset], drawData);
                        RenderTriangle(ref firstVertex, ref secondVertex, ref thirdVertex, clipLeft, clipTop, clipRight, clipBottom);
                    }

                    GL.End();
                }
            }

            GL.PopMatrix();
            return stats;
        }

        private static Color32 UnpackColor(uint packedColor)
        {
            return new Color32(
                (byte)(packedColor & 0xFFu),
                (byte)((packedColor >> 8) & 0xFFu),
                (byte)((packedColor >> 16) & 0xFFu),
                (byte)((packedColor >> 24) & 0xFFu));
        }

        private static DrawVertex BuildVertex(DearImguiNative.ImDrawVert vertex, DearImguiNative.ImDrawData* drawData)
        {
            return new DrawVertex
            {
                X = vertex.Pos.X - drawData->DisplayPos.X,
                Y = vertex.Pos.Y - drawData->DisplayPos.Y,
                U = vertex.Uv.X,
                V = vertex.Uv.Y,
                Color = UnpackColor(vertex.Col)
            };
        }

        private static void RenderTriangle(ref DrawVertex first, ref DrawVertex second, ref DrawVertex third, float clipLeft, float clipTop, float clipRight, float clipBottom)
        {
            var bufferA = new DrawVertex[8];
            var bufferB = new DrawVertex[8];
            bufferA[0] = first;
            bufferA[1] = second;
            bufferA[2] = third;
            var count = 3;
            count = ClipPolygonAgainstAxis(bufferA, count, bufferB, 0, clipLeft);
            count = ClipPolygonAgainstAxis(bufferB, count, bufferA, 1, clipRight);
            count = ClipPolygonAgainstAxis(bufferA, count, bufferB, 2, clipTop);
            count = ClipPolygonAgainstAxis(bufferB, count, bufferA, 3, clipBottom);
            if (count < 3)
            {
                return;
            }

            for (var i = 1; i < count - 1; i++)
            {
                EmitVertex(bufferA[0]);
                EmitVertex(bufferA[i]);
                EmitVertex(bufferA[i + 1]);
            }
        }

        private static int ClipPolygonAgainstAxis(DrawVertex[] input, int inputCount, DrawVertex[] output, int axis, float clipValue)
        {
            if (inputCount <= 0)
            {
                return 0;
            }

            var outputCount = 0;
            var previous = input[inputCount - 1];
            var previousInside = IsInside(previous, axis, clipValue);
            for (var i = 0; i < inputCount; i++)
            {
                var current = input[i];
                var currentInside = IsInside(current, axis, clipValue);
                if (currentInside != previousInside)
                {
                    output[outputCount++] = Intersect(previous, current, axis, clipValue);
                }

                if (currentInside)
                {
                    output[outputCount++] = current;
                }

                previous = current;
                previousInside = currentInside;
            }

            return outputCount;
        }

        private static bool IsInside(DrawVertex vertex, int axis, float clipValue)
        {
            switch (axis)
            {
                case 0: return vertex.X >= clipValue;
                case 1: return vertex.X <= clipValue;
                case 2: return vertex.Y >= clipValue;
                default: return vertex.Y <= clipValue;
            }
        }

        private static DrawVertex Intersect(DrawVertex start, DrawVertex end, int axis, float clipValue)
        {
            var startValue = axis < 2 ? start.X : start.Y;
            var endValue = axis < 2 ? end.X : end.Y;
            var denominator = endValue - startValue;
            var t = Mathf.Approximately(denominator, 0f) ? 0f : (clipValue - startValue) / denominator;
            t = Mathf.Clamp01(t);

            return new DrawVertex
            {
                X = Mathf.Lerp(start.X, end.X, t),
                Y = Mathf.Lerp(start.Y, end.Y, t),
                U = Mathf.Lerp(start.U, end.U, t),
                V = Mathf.Lerp(start.V, end.V, t),
                Color = LerpColor(start.Color, end.Color, t)
            };
        }

        private static Color32 LerpColor(Color32 start, Color32 end, float t)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(start.r + ((end.r - start.r) * t)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(start.g + ((end.g - start.g) * t)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(start.b + ((end.b - start.b) * t)), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(start.a + ((end.a - start.a) * t)), 0, 255));
        }

        private static void EmitVertex(DrawVertex vertex)
        {
            GL.Color(vertex.Color);
            GL.TexCoord2(vertex.U, vertex.V);
            GL.Vertex3(vertex.X, vertex.Y, 0f);
        }
    }
}
