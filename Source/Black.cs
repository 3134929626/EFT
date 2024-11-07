using MaterialSkin.Controls;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Vmmsharp.LeechCore;

namespace eft_dma_radar.Source
{
    public partial class Black : Form
    {
        private System.Windows.Forms.Timer refreshTimer;
        public Black()
        {
            
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.White;  // 背景为黑色
            InitializeComponent();
            //WindowState = FormWindowState.Normal;  // 初始状态为正常

            //refreshTimer = new System.Windows.Forms.Timer();
            //refreshTimer.Interval = 5; // 设置刷新间隔，单位为毫秒
            //refreshTimer.Tick += RefreshTimer_Tick;
            //refreshTimer.Start();

        }
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            skglControl1.Refresh(); // 定期刷新控件
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 如果存在第二个显示器
            if (Screen.AllScreens.Length > 1)
            {
                var screen = Screen.AllScreens.Where(x => x.DeviceName != Screen.FromControl(this).DeviceName).FirstOrDefault();
                // 设定窗口大小为第二个显示器的工作区域
                this.Location = screen.Bounds.Location;
                this.Size = screen.Bounds.Size;

            }
            else
            {
                MessageBox.Show("没有检测到第二个显示器。");
                //this.Close(); // 如果没有第二个显示器，就关闭窗口
            }
        }
        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    base.OnPaint(e);
        //    // 绘制一个矩形
        //    using (Brush brush = new SolidBrush(Color.Red))
        //    {
        //        e.Graphics.FillRectangle(brush, 0, 0, 200, 100); // 矩形位置和大小
        //    }
        //}
        private float D3DXVec3Dot(Vector3 a, Vector3 b)
        {
            return (a.X * b.X +
                    a.Y * b.Y +
                    a.Z * b.Z);
        }
        public bool WorldToScreen(Vector3 _Enemy, out Vector2 _Screen)
        {
            _Screen = new Vector2(0, 0);

            Matrik viewMatrix = _cameraManager.ViewMatrix;
            Matrik temp = Matrik.Transpose(viewMatrix);

            Vector3 translationVector = new Vector3(temp.M41, temp.M42, temp.M43);
            Vector3 up = new Vector3(temp.M21, temp.M22, temp.M23);
            Vector3 right = new Vector3(temp.M11, temp.M12, temp.M13);

            float w = D3DXVec3Dot(translationVector, _Enemy) + temp.M44;

            if (w < 0.098f)
            {
                return false;
            }

            // Calculate screen coordinates
            float y = D3DXVec3Dot(up, _Enemy) + temp.M24;
            float x = D3DXVec3Dot(right, _Enemy) + temp.M14;

            _Screen.X = (1920f / 2f) * (1f + x / w);
            _Screen.Y = (1080f / 2f) * (1f - y / w);

            return true;
        }
        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }
        private bool Ready
        {
            get => Memory.Ready;
        }
        private bool InGame
        {
            get => Memory.InGame;
        }

        private bool IsAtHideout
        {
            get => Memory.InHideout;
        }
        private Player LocalPlayer
        {
            get => Memory.Players?.FirstOrDefault(x => x.Value.Type is PlayerType.LocalPlayer).Value;
        }
        private LootManager Loot
        {
            get => Memory.Loot;
        }
        private QuestManager QuestManager
        {
            get => Memory.QuestManager;
        }
        private readonly Config _config;
        private bool IsReadyToRender()
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            bool isAtHideout = this.IsAtHideout;
            bool localPlayerExists = this.LocalPlayer is not null;

            if (!isReady)
                return false; // Game process not running

            if (isAtHideout)
                return false; // Main menu or hideout

            if (!inGame)
                return false; // Waiting for raid start

            if (!localPlayerExists)
                return false; // Cannot find local player

            return true; // Ready to render
        }
        private float NormalizeDirection(float direction)
        {
            var normalizedDirection = -direction;

            if (normalizedDirection < 0)
                normalizedDirection += 360;

            return normalizedDirection;
        }
        public void DrawStatusText(SKCanvas canvas)
        {
            bool isReady = this.Ready;
            bool inGame = this.InGame;
            bool isAtHideout = this.IsAtHideout;
            var localPlayer = this.LocalPlayer;

            string statusText;
            if (!isReady)
            {
                statusText = "游戏进程未运行";
            }
            else if (isAtHideout)
            {
                statusText = "主菜单或者隐藏...";
            }
            else if (!inGame)
            {

                statusText = "等待战局开始...";

            }
            else if (localPlayer is null)
            {
                statusText = "找不到本地玩家";
            }
            else
            {
                statusText = ""; // No status text to draw
            }

            var centerX = skglControl1.Width / 2;
            var centerY = skglControl1.Height / 2;

            //var index = SKFontManager.Default.FontFamilies.ToList().IndexOf("宋体");
            //创建宋体字形
            //var songtiTypeface = SKFontManager.Default.GetFontStyles(index).CreateTypeface(0);
            //SKTypeface.FromFamilyName("SimSun");

            SKPaint TextRadarStatus = new SKPaint
            {
                Color = SKColors.Red,
                TextSize = 14,
                Typeface = SKTypeface.FromFamilyName("宋体", SKTypefaceStyle.Bold),
                IsStroke = false,
                TextEncoding = SKTextEncoding.Utf8,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            //paint.TextEncoding = SKTextEncoding.Utf8;

            canvas.DrawText(statusText, centerX, 14, TextRadarStatus);
            


        }
        private float CalculatePitch(float pitch)
        {
            if (pitch >= 270)
                return 360 - pitch;
            else
                return -pitch;
        }
        private bool ShouldDrawLootObject(Vector3 myPosition, Vector3 lootPosition, float maxDistance)
        {
            var dist = Vector3.Distance(myPosition, lootPosition);
            return dist <= maxDistance;
        }
        private float CalculateCircleSize(float dist)
        {
            return (float)((31.6437 - 5.09664 * Math.Log(0.591394 * dist + 70.0756)) * 0.5);
        }
        private void RenderAimview(SKCanvas canvas, Player sourcePlayer)
        {
            if (sourcePlayer is null || !sourcePlayer.IsActive || !sourcePlayer.IsAlive)
                return;

            //Vector3 temp = GetHead(this.LocalPlayer);
            var myPosition = sourcePlayer.BonePositions;
            var myRotation = sourcePlayer.Rotation;
            var normalizedDirection = NormalizeDirection(myRotation.X);
            var pitch = CalculatePitch(myRotation.Y);


            var loot = this.Loot; // cache ref
            if (loot is not null && loot.Filter is not null)
            {
                foreach (var item in loot.Filter)
                {
                    if (item is null || (this._config.ImportantLootOnly && !item.Important && !item.AlwaysShow && !item.RequiredByQuest))
                        continue;

                    if (ShouldDrawLootObject(myPosition, item.Position, _config.MaxDistance))
                        DrawLootableObject(canvas, myPosition, sourcePlayer.ZoomedPosition, item, normalizedDirection, pitch);
                }
            }
            //var Questloot = this.QuestManager.QuestItems.Where(x => x?.Position.X != 0 && x?.Name != "????");
            //if (Questloot is not null)
            //{
            //    foreach (var item in Questloot)
            //    {
            //        if (item is null || item.Complete)
            //            continue;
            //
            //        if (ShouldDrawLootObject(myPosition, item.Position, _config.MaxDistance))
            //            DrawQuestableObject(canvas, drawingLocation, myPosition, sourcePlayer.ZoomedPosition, item, normalizedDirection, pitch);
            //    }
            //}
        }
        private void DrawLootableObject(SKCanvas canvas, Vector3 myPosition, Vector2 myZoomedPos, LootableObject lootableObject, float normalizedDirection, float pitch)
        {
            SKPaint TextRadarStatus = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 13,
                Typeface = SKTypeface.FromFamilyName("宋体", SKTypefaceStyle.Bold),
                IsStroke = false,
                TextEncoding = SKTextEncoding.Utf8,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            var paint = Extensions.GetEntityPaint(lootableObject);
            paint.TextSize = 14;
            paint.TextAlign = SKTextAlign.Center;

            var lootableObjectPos = lootableObject.Position;
            lootableObjectPos.Z = lootableObjectPos.Z + 0.1f;

            float opposite = lootableObjectPos.Y - myPosition.Y;
            float adjacent = lootableObjectPos.X - myPosition.X;
            float dist = (float)Math.Sqrt(opposite * opposite + adjacent * adjacent);
            //float dist = Vector3.Distance(myPosition, lootableObjectPos);
            if (dist > 18f)
            {
                return;
            }


            if (WorldToScreen(lootableObject.Position, out Vector2 scrpos))
            {
                float circleSize = CalculateCircleSize(dist);
                canvas.DrawCircle(scrpos.X, scrpos.Y, circleSize * frmMain._uiScale, paint);//SKPaints.LootPaint

                canvas.DrawText(lootableObject.Name, scrpos.X, scrpos.Y - circleSize * frmMain._uiScale / 2 - 2, paint);

                canvas.DrawText("[" + Math.Round(dist, 1) + "m]", scrpos.X, scrpos.Y + circleSize * frmMain._uiScale / 2 + paint.TextSize + 2, paint);
            }
        }
        private void skglControl1_PaintSurface(object sender, SkiaSharp.Views.Desktop.SKPaintGLSurfaceEventArgs e)
        {
            try
            {
                SKCanvas canvas = e.Surface.Canvas;
                canvas.Clear();
                //if (IsReadyToRender())
                //{
                //    //lock (frmMain._renderLock)
                //    //{
                //        RenderAimview(canvas, this.LocalPlayer);
                //    //}
                //}
                //else
                //{
                //
                //    DrawStatusText(canvas);
                //}
                canvas.Flush();
            }
            catch { }
        }
    }
}
