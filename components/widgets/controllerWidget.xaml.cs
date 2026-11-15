using DualSenseBatteryMonitor.components.enums;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DualSenseBatteryMonitor
{
    public partial class controllerWidget : Page
    {
        private readonly int self_index;

        private const float enabled_opacity = 1.0f;
        private const float disabled_opacity = 0.4f;

        private static readonly DrawingImage ControllerDualSense = (DrawingImage)System.Windows.Application.Current.FindResource("controller_DualSenseDrawingImage");
        private static readonly DrawingImage ControllerDualSenseEdge = (DrawingImage)System.Windows.Application.Current.FindResource("controller_DualSenseEdgeDrawingImage");

        private static readonly DrawingImage connectionTypeIcon_Unknown = (DrawingImage)System.Windows.Application.Current.FindResource("connection_UnknownDrawingImage");
        private static readonly DrawingImage connectionTypeIcon_USB = (DrawingImage)System.Windows.Application.Current.FindResource("connection_USBDrawingImage");
        private static readonly DrawingImage connectionTypeIcon_Bluetooth = (DrawingImage)System.Windows.Application.Current.FindResource("connection_BluetoothDrawingImage");

        private static readonly Dictionary<int, LinearGradientBrush> gradientCache = new();

        private static readonly SolidColorBrush HudWhiteBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush HudCriticalRedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF3B30"));
        private static readonly SolidColorBrush HudFullGreenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1CE885"));

        private readonly LinearGradientBrush color_player_01 = new LinearGradientBrush();
        private readonly LinearGradientBrush color_player_02 = new LinearGradientBrush();
        private readonly LinearGradientBrush color_player_03 = new LinearGradientBrush();
        private readonly LinearGradientBrush color_player_04 = new LinearGradientBrush();
        private List<Brush>? playerColors;

        private bool isPlayingLowBatAnim = false;
        private string activeChargingStoryboardKey = "";
        private const int lowBatteryThreshold = 15;
        private readonly Storyboard blink_storyboard;

        private bool showBatteryInPercentage = App.GetShowBatteryInPercentageSetting();

        private int lastControllerAmount;
        private int lastBatteryLevel;
        private bool lastIsCharging;
        private ConnectionTypeEnum lastConnectionType;
        private bool lastIsEdge;
        private TimeSpan? lastDrainEstimate;
        private bool hasRefreshData = false;

        static controllerWidget()
        {
            HudWhiteBrush.Freeze();
            HudCriticalRedBrush.Freeze();
            HudFullGreenBrush.Freeze();
        }

        public controllerWidget(bool noControllers, int index, int batterylevel)
        {
            self_index = index + 1;

            InitializePlayerColors();
            InitializeComponent();

            blink_storyboard = (Storyboard)Resources["BlinkStoryboard"];

            updatePlayerColor();
            count_index.Text = self_index.ToString();

            App.BatteryStatVisibilityChanged += OnBatteryStatVisibilityChanged;
            App.BatteryStatFileDeleted += OnBatteryStatFileDeleted;
            App.BatteryInPercentageChanged += BatteryInPercentageChanged;
        }

        private void BatteryInPercentageChanged()
        {
            showBatteryInPercentage = App.GetShowBatteryInPercentageSetting();
            if (hasRefreshData) RefreshData(lastControllerAmount, lastBatteryLevel, lastIsCharging, lastConnectionType, lastIsEdge, lastDrainEstimate);
        }

        private void OnBatteryStatVisibilityChanged()
        {
            if (PossibilyDisableBatteryStats()) return;

            if (App.batteryDrainStatsErrorCode <= App.batteryErrorCodeTrehsold)
            {
                text_drainstat_average.Visibility = App.GetShowBatteryStatsTimeEstimateSetting() ? Visibility.Visible : Visibility.Collapsed;
                text_drainstat_Deco02.Visibility = App.GetShowBatteryStatsTimeLeftSetting() ? Visibility.Visible : Visibility.Collapsed;
                text_drainstat_left.Visibility = App.GetShowBatteryStatsTimeLeftSetting() ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnBatteryStatFileDeleted()
        {
            ResetBatteryStatText();
        }

        private bool PossibilyDisableBatteryStats()
        {
            bool disable = App.GetDontSaveBatteryStatsSetting();
            if (disable)
            {
                text_drainstat_error.Visibility = Visibility.Collapsed;
                text_drainstat_average.Visibility = Visibility.Collapsed;
                text_drainstat_left.Visibility = Visibility.Collapsed;
                text_drainstat_Deco02.Visibility = Visibility.Collapsed;
            }

            return disable;
        }

        private void SetDualSenseIcon(bool isEdge)
        {
            if (isEdge)
            {
                if (image_controller_icon.Source != ControllerDualSenseEdge) image_controller_icon.Source = ControllerDualSenseEdge;
            }
            else
            {
                if (image_controller_icon.Source != ControllerDualSense) image_controller_icon.Source = ControllerDualSense;
            }
        }

        public void RefreshData(int controllerAmount, int batterylevel, bool isCharging, ConnectionTypeEnum ConnectionType, bool isEdge)
        {
            RefreshData(controllerAmount, batterylevel, isCharging, ConnectionType, isEdge, null);
        }

        public void RefreshData(int controllerAmount, int batterylevel, bool isCharging, ConnectionTypeEnum ConnectionType, bool isEdge, TimeSpan? drainEstimate)
        {
            if (controllerAmount == 0)
            {
                if (self_index != 0)
                {
                    Visibility = Visibility.Collapsed;
                    UpdateBatteryAnim(0, true);
                }
                else
                {
                    Visibility = Visibility.Visible;
                    SetDualSenseIcon(isEdge);
                    ps5_hud_container.Opacity = disabled_opacity;
                    UpdatePs5HudVisuals(0, false, ConnectionType);
                    SetChargingIconActive(isCharging);
                    SetConnectionTypeIcon(ConnectionType);
                    UpdateBatteryAnim(batterylevel, true);
                }
            }
            else
            {
                if (controllerAmount >= self_index)
                {
                    Visibility = Visibility.Visible;
                    SetDualSenseIcon(isEdge);
                    ps5_hud_container.Opacity = enabled_opacity;

                    count_index.Visibility = controllerAmount > 1 ? Visibility.Visible : Visibility.Collapsed;

                    if (batterylevel > App.batteryErrorCodeTrehsold)
                    {
                        ps5_battery_frame.Visibility = Visibility.Hidden;
                        ps5_battery_nub.Visibility = Visibility.Hidden;
                        SetChargingIconActive(false);
                        SetConnectionTypeIcon(ConnectionType);
                        UpdateBatteryAnim(batterylevel, false);
                        SetDebugErrorCode(batterylevel, true);
                        UpdateDrainStatLabel();
                    }
                    else
                    {
                        ps5_battery_frame.Visibility = Visibility.Visible;
                        ps5_battery_nub.Visibility = Visibility.Visible;

                        UpdatePs5HudVisuals(batterylevel, isCharging, ConnectionType);

                        text_battery_level_alt.Text = $"{batterylevel}";
                        text_battery_level_alt.Visibility = showBatteryInPercentage ? Visibility.Visible : Visibility.Collapsed;
                        text_battery_level_altPerc.Visibility = showBatteryInPercentage ? Visibility.Visible : Visibility.Collapsed;

                        SetChargingIconActive(isCharging);
                        SetConnectionTypeIcon(ConnectionType);
                        UpdateBatteryAnim(batterylevel, isCharging);
                        SetDebugErrorCode(0, false);
                        UpdateDrainStatLabel(drainEstimate, batterylevel);
                    }
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                    UpdateBatteryAnim(0, true);
                    UpdateDrainStatLabel();
                }
            }

            lastControllerAmount = controllerAmount;
            lastBatteryLevel = batterylevel;
            lastIsCharging = isCharging;
            lastConnectionType = ConnectionType;
            lastIsEdge = isEdge;
            lastDrainEstimate = drainEstimate;
            hasRefreshData = true;
        }

        private void UpdatePs5HudVisuals(int batterylevel, bool isCharging, ConnectionTypeEnum connectionType)
        {
            bool isCritical = batterylevel <= lowBatteryThreshold && !isCharging;
            bool isFullOnCable = batterylevel >= 100 && (isCharging || connectionType == ConnectionTypeEnum.USB);

            SolidColorBrush frameAndControllerBrush = isCritical ? HudCriticalRedBrush : HudWhiteBrush;
            SolidColorBrush barsBrush = isCritical ? HudCriticalRedBrush : (isFullOnCable ? HudFullGreenBrush : HudWhiteBrush);

            ps5_controller_path.Fill = frameAndControllerBrush;
            ps5_battery_nub.Fill = frameAndControllerBrush;
            ps5_battery_frame.BorderBrush = frameAndControllerBrush;
            text_battery_level_alt.Foreground = frameAndControllerBrush;
            text_battery_level_altPerc.Foreground = frameAndControllerBrush;

            bar_1.Fill = barsBrush;
            bar_2.Fill = barsBrush;
            bar_3.Fill = barsBrush;
            bar_4.Fill = barsBrush;

            string targetStoryboardKey = "";

            if (isCritical)
            {
                bar_4.Visibility = Visibility.Hidden;
                bar_3.Visibility = Visibility.Hidden;
                bar_2.Visibility = Visibility.Hidden;
                bar_1.Visibility = Visibility.Hidden;
            }
            else if (isFullOnCable)
            {
                bar_4.Visibility = Visibility.Visible;
                bar_3.Visibility = Visibility.Visible;
                bar_2.Visibility = Visibility.Visible;
                bar_1.Visibility = Visibility.Visible;
                targetStoryboardKey = "BlinkAllBarsFull";
            }
            else if (isCharging)
            {
                if (batterylevel < 25)
                {
                    bar_4.Visibility = Visibility.Visible;
                    bar_3.Visibility = Visibility.Hidden;
                    bar_2.Visibility = Visibility.Hidden;
                    bar_1.Visibility = Visibility.Hidden;
                    targetStoryboardKey = "BlinkBar4";
                }
                else if (batterylevel < 50)
                {
                    bar_4.Visibility = Visibility.Visible;
                    bar_3.Visibility = Visibility.Visible;
                    bar_2.Visibility = Visibility.Hidden;
                    bar_1.Visibility = Visibility.Hidden;
                    targetStoryboardKey = "BlinkBar3";
                }
                else if (batterylevel < 75)
                {
                    bar_4.Visibility = Visibility.Visible;
                    bar_3.Visibility = Visibility.Visible;
                    bar_2.Visibility = Visibility.Visible;
                    bar_1.Visibility = Visibility.Hidden;
                    targetStoryboardKey = "BlinkBar2";
                }
                else
                {
                    bar_4.Visibility = Visibility.Visible;
                    bar_3.Visibility = Visibility.Visible;
                    bar_2.Visibility = Visibility.Visible;
                    bar_1.Visibility = Visibility.Visible;
                    targetStoryboardKey = "BlinkBar1";
                }
            }
            else
            {
                bar_4.Visibility = batterylevel > 0 ? Visibility.Visible : Visibility.Hidden;
                bar_3.Visibility = batterylevel >= 25 ? Visibility.Visible : Visibility.Hidden;
                bar_2.Visibility = batterylevel >= 50 ? Visibility.Visible : Visibility.Hidden;
                bar_1.Visibility = batterylevel >= 75 ? Visibility.Visible : Visibility.Hidden;
            }

            if (activeChargingStoryboardKey != targetStoryboardKey)
            {
                if (!string.IsNullOrEmpty(activeChargingStoryboardKey))
                {
                    ((Storyboard)Resources[activeChargingStoryboardKey]).Stop(this);
                }

                bar_1.Opacity = 1.0;
                bar_2.Opacity = 1.0;
                bar_3.Opacity = 1.0;
                bar_4.Opacity = 1.0;

                if (!string.IsNullOrEmpty(targetStoryboardKey))
                {
                    ((Storyboard)Resources[targetStoryboardKey]).Begin(this, true);
                }

                activeChargingStoryboardKey = targetStoryboardKey;
            }
        }

        private void UpdateDrainStatLabel()
        {
            UpdateDrainStatLabel(null, -1);
        }

        private void UpdateDrainStatLabel(TimeSpan? estimate, int currentBatteryLevel)
        {
            if (PossibilyDisableBatteryStats()) return;

            bool hasError = App.batteryDrainStatsErrorCode > App.batteryErrorCodeTrehsold;
            bool showStats = estimate != null && !hasError;

            ChangeStatsErrorAndStatsVisibility(hasError);
            if (hasError) return;

            Visibility newVisTimeLeft = App.GetShowBatteryStatsTimeLeftSetting() ? Visibility.Visible : Visibility.Collapsed;
            Visibility newVisTimeEstimate = App.GetShowBatteryStatsTimeEstimateSetting() ? Visibility.Visible : Visibility.Collapsed;
            if (showStats)
            {
                TimeSpan remaining = TimeSpan.FromMinutes(estimate!.Value.TotalMinutes * (currentBatteryLevel / 100.0));
                text_drainstat_average.Text = $"~{(int)estimate.Value.TotalHours:D2}h {estimate.Value.Minutes:D2}m";
                text_drainstat_left.Text = $"~{(int)remaining.TotalHours:D2}h {remaining.Minutes:D2}m";
            }
            else
            {
                ResetBatteryStatText();
            }

            if (text_drainstat_average.Visibility != newVisTimeEstimate)
            {
                text_drainstat_average.Visibility = newVisTimeEstimate;
            }

            if (text_drainstat_left.Visibility != newVisTimeLeft)
            {
                text_drainstat_left.Visibility = newVisTimeLeft;
                text_drainstat_Deco02.Visibility = newVisTimeLeft;
            }
        }

        private void ChangeStatsErrorAndStatsVisibility(bool hasError)
        {
            Visibility newVisError = hasError ? Visibility.Visible : Visibility.Collapsed;
            Visibility otherVis = newVisError == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (text_drainstat_error.Visibility != newVisError)
            {
                text_drainstat_error.Visibility = newVisError;
                text_drainstat_average.Visibility = otherVis;
                text_drainstat_left.Visibility = otherVis;
                text_drainstat_Deco02.Visibility = otherVis;
            }
        }

        private void ResetBatteryStatText()
        {
            text_drainstat_average.Text = "~--h --m";
            text_drainstat_left.Text = "~--h --m";
        }

        private void InitializePlayerColors()
        {
            color_player_01.StartPoint = new Point(0.5, 0);
            color_player_01.EndPoint = new Point(0.5, 1);
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00bfff"), 0.0));
            color_player_01.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#007399"), 1.0));
            color_player_01.Freeze();

            color_player_02.StartPoint = new Point(0.5, 0);
            color_player_02.EndPoint = new Point(0.5, 1);
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff0000"), 0.0));
            color_player_02.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990000"), 1.0));
            color_player_02.Freeze();

            color_player_03.StartPoint = new Point(0.5, 0);
            color_player_03.EndPoint = new Point(0.5, 1);
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00ff40"), 0.0));
            color_player_03.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#009926"), 1.0));
            color_player_03.Freeze();

            color_player_04.StartPoint = new Point(0.5, 0);
            color_player_04.EndPoint = new Point(0.5, 1);
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#ff00ff"), 0.0));
            color_player_04.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#990099"), 1.0));
            color_player_04.Freeze();

            playerColors = new List<Brush> { color_player_01, color_player_02, color_player_03, color_player_04 };
        }

        private void UpdateBatteryAnim(int batterylevel, bool isCharging)
        {
            if (batterylevel <= lowBatteryThreshold)
            {
                if (!isCharging)
                {
                    if (!isPlayingLowBatAnim)
                    {
                        blink_storyboard.Begin(this, true);
                        isPlayingLowBatAnim = true;
                    }
                }
                else
                {
                    if (isPlayingLowBatAnim)
                    {
                        blink_storyboard.Stop(this);
                    }
                    isPlayingLowBatAnim = false;
                }
            }
            else
            {
                if (isPlayingLowBatAnim)
                {
                    blink_storyboard.Stop(this);
                }
                isPlayingLowBatAnim = false;
            }
        }

        private void updatePlayerColor()
        {
            if (self_index >= 1 && playerColors != null && self_index <= playerColors.Count)
                count_index.Foreground = playerColors[self_index - 1];
        }

        private void SetChargingIconActive(bool charging)
        {
            ps5_charging_bolt.Visibility = charging ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetConnectionTypeIcon(ConnectionTypeEnum connectionType)
        {
            switch (connectionType)
            {
                case ConnectionTypeEnum.USB:
                    if (icon_connectiontype.Source != connectionTypeIcon_USB) icon_connectiontype.Source = connectionTypeIcon_USB;
                    break;
                case ConnectionTypeEnum.Bluetooth:
                    if (icon_connectiontype.Source != connectionTypeIcon_Bluetooth) icon_connectiontype.Source = connectionTypeIcon_Bluetooth;
                    break;
                default:
                    if (icon_connectiontype.Source != connectionTypeIcon_Unknown) icon_connectiontype.Source = connectionTypeIcon_Unknown;
                    break;
            }
        }

        private void SetDebugErrorCode(int errorCode, bool shouldShow)
        {
            if (shouldShow)
            {
                debug_errorcode_text.Text = errorCode.ToString();
                if (debug_errorcode_text.Visibility == Visibility.Collapsed)
                {
                    debug_errorcode_text.Visibility = Visibility.Visible;
                    debug_errorcode_text_text.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (debug_errorcode_text.Visibility == Visibility.Visible)
                {
                    debug_errorcode_text.Visibility = Visibility.Collapsed;
                    debug_errorcode_text_text.Visibility = Visibility.Collapsed;
                }
            }
        }

        private static LinearGradientBrush HueToGradient(double hue)
        {
            int key = (int)Math.Round(hue);
            if (!gradientCache.TryGetValue(key, out LinearGradientBrush? brush))
            {
                brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                brush.GradientStops.Add(new GradientStop(ColorFromHue(hue, 0.7), 0.0));
                brush.GradientStops.Add(new GradientStop(ColorFromHue(hue, 0.9), 1.0));
                brush.Freeze();
                gradientCache[key] = brush;
            }
            return brush;
        }

        private static Color ColorFromHue(double hue, double darkness)
        {
            hue = hue % 360;
            if (hue < 0) hue += 360;
            hue = 0 - (40 - (hue * 2));
            hue = Math.Clamp(hue, 0, 100);

            double s = 1.0;
            double v = darkness;

            int hi = (int)(hue / 60) % 6;
            double f = (hue / 60) - Math.Floor(hue / 60);

            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r = 0, g = 0, b = 0;

            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }
}
