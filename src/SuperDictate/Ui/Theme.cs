using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace SuperDictate.Ui;

/// <summary>
/// Every color and control template of the settings and setup windows, in
/// Telegram's night theme: a dark sidebar with a solid blue selected page,
/// rounded cards on a deep blue-grey field, blue card titles and soft-blue
/// buttons. Views reference brushes by key (Bg, Card, Text, Muted, Accent,
/// Link, ...) and never build their own, so the whole look changes here.
/// Every text pair meets WCAG AA (4.5:1) and every control outline 3:1, which
/// the self-test checks.
/// </summary>
internal static class Theme
{
    public const string IconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>
    /// Telegram's night palette, with the accent deepened so white text on it
    /// meets WCAG AA; every text pair meets 4.5:1 and every control outline 3:1,
    /// which the self-test checks.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Palette = new Dictionary<string, string>
    {
        // The field the cards sit on, and the sidebar.
        ["Bg"] = "#0E1621",
        ["Side"] = "#17212B",
        ["Card"] = "#17212B",
        ["Field"] = "#242F3D",
        // Hover fill, slider and progress tracks, tiles.
        ["Control"] = "#202B36",
        ["Stroke"] = "#243140",
        ["StrokeHover"] = "#34424F",
        ["Text"] = "#F5F5F5",
        ["Muted"] = "#8395A7",
        ["Raised"] = "#1E2A36",
        // Fills behind white text: the selected page, primary buttons, switches.
        ["Accent"] = "#3272AA",
        // Blue text: card titles and the soft buttons.
        ["Link"] = "#6AB2F2",
        ["AccentSoft"] = "#1F3448",
        ["Ok"] = "#4FCB7C",
        ["Warn"] = "#F5B14C",
        ["Bad"] = "#F25C63",
        // Fill behind white text (Stop button); Bad itself is for text and dots.
        ["BadFill"] = "#C93A42",
        ["BadSoft"] = "#301A1E",
        // Hover lightens a control a touch; a press darkens it, which keeps white text above 4.5:1.
        ["Wash"] = "#FFFFFF",
        ["Shade"] = "#000000",
    };

    public static string Color(string key) => Palette[key];

    /// <summary>Windows draws the title bar of a standard window; this makes it dark to match.</summary>
    public static void UseDarkTitleBar(Window window) => window.SourceInitialized += (_, _) =>
    {
        var on = 1;
        // Older Windows ignores the attribute and keeps its own title bar.
        Interop.NativeMethods.DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(window).Handle,
            Interop.NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    };

    public static ResourceDictionary Create()
    {
        // Quick: feedback arriving under the pointer. Settle: everything easing back.
        // With Windows' animation effects off, every change lands at once.
        var motion = SystemParameters.ClientAreaAnimation;
        var entries = Palette.Select(pair => $"<SolidColorBrush x:Key='{pair.Key}' Color='{pair.Value}'/>")
            .Append($"<Duration x:Key='Quick'>{(motion ? "0:0:0.1" : "0:0:0")}</Duration>")
            .Append($"<Duration x:Key='Settle'>{(motion ? "0:0:0.24" : "0:0:0")}</Duration>");
        return (ResourceDictionary)XamlReader.Parse(Xaml.Replace("<!-- palette -->", string.Join("\n    ", entries)));
    }

    /// <summary>
    /// Motion follows two rules. Feedback starts on the press, not the release.
    /// Every animation starts from where the control is on screen, so moving
    /// the pointer off or flipping a switch back mid-move never jumps.
    /// </summary>
    private const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <!-- palette -->
    <!-- Fast start, soft landing: close to a critically damped spring. -->
    <CubicEase x:Key='Out' EasingMode='EaseOut'/>

    <!-- Pressable controls name their parts Hover (light wash), Down (shade) and Root
         (the part that sinks), and run these. None sets From, so each one starts
         from wherever the last left off. -->
    <Storyboard x:Key='HoverIn'>
        <DoubleAnimation Storyboard.TargetName='Hover' Storyboard.TargetProperty='Opacity' To='0.06' Duration='{StaticResource Quick}'/>
    </Storyboard>
    <Storyboard x:Key='HoverOut'>
        <DoubleAnimation Storyboard.TargetName='Hover' Storyboard.TargetProperty='Opacity' To='0' Duration='{StaticResource Settle}'/>
    </Storyboard>
    <!-- The press shows at once; only the release fades. -->
    <Storyboard x:Key='ShadeIn'>
        <DoubleAnimation Storyboard.TargetName='Down' Storyboard.TargetProperty='Opacity' To='0.16' Duration='0:0:0'/>
    </Storyboard>
    <Storyboard x:Key='ShadeOut'>
        <DoubleAnimation Storyboard.TargetName='Down' Storyboard.TargetProperty='Opacity' To='0' Duration='{StaticResource Settle}'/>
    </Storyboard>
    <Storyboard x:Key='SinkIn'>
        <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                         To='0.97' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
        <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                         To='0.97' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
    </Storyboard>
    <Storyboard x:Key='SinkOut'>
        <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                         To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
        <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                         To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
    </Storyboard>

    <!-- Focus rings. Windows draws a FocusVisualStyle only when focus arrives from the
         keyboard, so a ring marks where Tab went and never lingers after a click. -->
    <Style x:Key='FocusRing'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Border Margin='-3' CornerRadius='13' BorderThickness='2' BorderBrush='{StaticResource Accent}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='FocusPill'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Border Margin='-3' CornerRadius='14' BorderThickness='2' BorderBrush='{StaticResource Accent}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <!-- On the sidebar the ring sits on the item itself, light so it shows on the blue pill. -->
    <Style x:Key='FocusInset'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Border CornerRadius='10' BorderThickness='2' BorderBrush='{StaticResource Text}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='FocusField'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Border CornerRadius='10' BorderThickness='2' BorderBrush='{StaticResource Accent}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='FocusTile'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Border Margin='-3' CornerRadius='15' BorderThickness='2' BorderBrush='{StaticResource Accent}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='FocusDot'>
        <Setter Property='Control.Template'>
            <Setter.Value>
                <ControlTemplate>
                    <Ellipse Margin='-3' Stroke='{StaticResource Accent}' StrokeThickness='2'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Buttons are soft blue with blue text; one solid blue button per job (AccentButton).
         Hover fades a light wash in. Pressing darkens the button at once and sinks it
         to 97 %; letting go eases it back. -->
    <Style TargetType='Button'>
        <Setter Property='Foreground' Value='{StaticResource Link}'/>
        <Setter Property='Background' Value='{StaticResource AccentSoft}'/>
        <Setter Property='BorderBrush' Value='Transparent'/>
        <Setter Property='Height' Value='34'/>
        <Setter Property='Padding' Value='16,0'/>
        <Setter Property='FontSize' Value='13'/>
        <Setter Property='FontWeight' Value='SemiBold'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRing}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='Button'>
                    <Grid x:Name='Root' RenderTransformOrigin='0.5,0.5'>
                        <Grid.RenderTransform><ScaleTransform/></Grid.RenderTransform>
                        <Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                                BorderThickness='1' CornerRadius='10' SnapsToDevicePixels='True'>
                            <Grid>
                                <Border x:Name='Hover' Background='{StaticResource Wash}' Opacity='0' CornerRadius='9'/>
                                <Border x:Name='Down' Background='{StaticResource Shade}' Opacity='0' CornerRadius='9'/>
                                <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                            </Grid>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource HoverIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource HoverOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions>
                                <BeginStoryboard Storyboard='{StaticResource ShadeIn}'/>
                                <BeginStoryboard Storyboard='{StaticResource SinkIn}'/>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard Storyboard='{StaticResource ShadeOut}'/>
                                <BeginStoryboard Storyboard='{StaticResource SinkOut}'/>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsEnabled' Value='False'>
                            <Setter Property='Opacity' Value='0.45'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style x:Key='AccentButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
        <Setter Property='Background' Value='{StaticResource Accent}'/>
        <Setter Property='Foreground' Value='White'/>
    </Style>
    <!-- Second click of a two-step delete. -->
    <Style x:Key='DangerButton' TargetType='Button' BasedOn='{StaticResource AccentButton}'>
        <Setter Property='Background' Value='{StaticResource BadFill}'/>
    </Style>
    <Style x:Key='GhostButton' TargetType='Button' BasedOn='{StaticResource {x:Type Button}}'>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Foreground' Value='{StaticResource Muted}'/>
        <Setter Property='FontFamily' Value='Segoe Fluent Icons, Segoe MDL2 Assets'/>
        <Setter Property='FontWeight' Value='Normal'/>
        <Setter Property='FontSize' Value='12'/>
        <Setter Property='Width' Value='34'/>
        <Setter Property='Padding' Value='0'/>
    </Style>
    <Style x:Key='CloseButton' TargetType='Button' BasedOn='{StaticResource GhostButton}'>
        <Style.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
                <Setter Property='Background' Value='#D5392F'/>
                <Setter Property='Foreground' Value='White'/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Text inputs share one template: a hairline box that turns into a 2px blue one on focus. -->
    <ControlTemplate x:Key='FieldTemplate' TargetType='Control'>
        <Grid>
            <Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                    BorderThickness='1' CornerRadius='10' SnapsToDevicePixels='True'>
                <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/>
            </Border>
            <Border x:Name='Focus' BorderBrush='{StaticResource Accent}' BorderThickness='2' CornerRadius='10'
                    IsHitTestVisible='False' Visibility='Hidden'/>
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
                <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Muted}'/>
            </Trigger>
            <Trigger Property='IsKeyboardFocusWithin' Value='True'>
                <Setter TargetName='Focus' Property='Visibility' Value='Visible'/>
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>
    <Style x:Key='FieldStyle' TargetType='Control'>
        <Setter Property='Background' Value='{StaticResource Field}'/>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='BorderBrush' Value='{StaticResource StrokeHover}'/>
        <Setter Property='MinHeight' Value='34'/>
        <Setter Property='Padding' Value='8,5'/>
        <Setter Property='FontSize' Value='13'/>
        <Setter Property='VerticalContentAlignment' Value='Center'/>
        <Setter Property='Template' Value='{StaticResource FieldTemplate}'/>
    </Style>
    <Style TargetType='TextBox' BasedOn='{StaticResource FieldStyle}'>
        <Setter Property='CaretBrush' Value='{StaticResource Text}'/>
        <Setter Property='SelectionBrush' Value='{StaticResource Accent}'/>
    </Style>
    <Style TargetType='PasswordBox' BasedOn='{StaticResource FieldStyle}'>
        <Setter Property='CaretBrush' Value='{StaticResource Text}'/>
        <Setter Property='SelectionBrush' Value='{StaticResource Accent}'/>
    </Style>

    <!-- Boolean settings are Telegram switches. Pressing grows the knob a little;
         flipping slides it across while the blue fill fades in under it. -->
    <Style TargetType='CheckBox'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusPill}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='CheckBox'>
                    <StackPanel Orientation='Horizontal' Background='Transparent'>
                        <Grid Width='40' Height='22' VerticalAlignment='Center'>
                            <Border x:Name='Track' CornerRadius='11' Background='{StaticResource Control}'
                                    BorderBrush='{StaticResource Muted}' BorderThickness='1.5'/>
                            <Border x:Name='On' CornerRadius='11' Background='{StaticResource Accent}' Opacity='0'/>
                            <Grid x:Name='Knob' Width='14' Height='14' Margin='4,0,0,0' HorizontalAlignment='Left'>
                                <Grid.RenderTransform><TranslateTransform/></Grid.RenderTransform>
                                <Grid x:Name='Face' RenderTransformOrigin='0.5,0.5'>
                                    <Grid.RenderTransform><ScaleTransform/></Grid.RenderTransform>
                                    <Ellipse Fill='{StaticResource Muted}'/>
                                    <Ellipse Fill='White' Opacity='{Binding Opacity, ElementName=On}'/>
                                </Grid>
                            </Grid>
                        </Grid>
                        <ContentPresenter x:Name='Label' Margin='10,0,0,0' VerticalAlignment='Center'/>
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property='HasContent' Value='False'>
                            <Setter TargetName='Label' Property='Visibility' Value='Collapsed'/>
                        </Trigger>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Track' Property='BorderBrush' Value='{StaticResource Text}'/>
                        </Trigger>
                        <!-- The resting state, so a switch that opens checked is simply on. -->
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='On' Property='Opacity' Value='1'/>
                            <Setter TargetName='Knob' Property='Margin' Value='22,0,0,0'/>
                        </Trigger>
                        <!-- A flip jumps to the new resting state, and an additive animation starts
                             at the old one and fades to nothing. Composed, the animations add up:
                             a switch flipped back mid-slide turns around where it is. -->
                        <EventTrigger RoutedEvent='ToggleButton.Checked'>
                            <BeginStoryboard HandoffBehavior='Compose'>
                                <Storyboard FillBehavior='Stop'>
                                    <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)'
                                                     From='-18' To='0' IsAdditive='True' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                    <DoubleAnimation Storyboard.TargetName='On' Storyboard.TargetProperty='Opacity'
                                                     From='-1' To='0' IsAdditive='True' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                </Storyboard>
                            </BeginStoryboard>
                        </EventTrigger>
                        <EventTrigger RoutedEvent='ToggleButton.Unchecked'>
                            <BeginStoryboard HandoffBehavior='Compose'>
                                <Storyboard FillBehavior='Stop'>
                                    <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)'
                                                     From='18' To='0' IsAdditive='True' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                    <DoubleAnimation Storyboard.TargetName='On' Storyboard.TargetProperty='Opacity'
                                                     From='1' To='0' IsAdditive='True' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                </Storyboard>
                            </BeginStoryboard>
                        </EventTrigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName='Face' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                                         To='1.2' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
                                        <DoubleAnimation Storyboard.TargetName='Face' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                                         To='1.2' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName='Face' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                                         To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                        <DoubleAnimation Storyboard.TargetName='Face' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                                         To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsEnabled' Value='False'>
                            <Setter Property='Opacity' Value='0.45'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Sidebar pages, like Telegram's chat list: the selected one is a solid blue pill. -->
    <Style x:Key='Nav' TargetType='RadioButton'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusInset}'/>
        <Setter Property='Margin' Value='0,2'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RadioButton'>
                    <Grid Height='42'>
                        <Border x:Name='Bd' CornerRadius='10' Background='Transparent'/>
                        <Border x:Name='Hover' CornerRadius='10' Background='{StaticResource Wash}' Opacity='0'/>
                        <Border x:Name='Down' CornerRadius='10' Background='{StaticResource Shade}' Opacity='0'/>
                        <ContentPresenter Margin='12,0' VerticalAlignment='Center'
                                          HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource HoverIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource HoverOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource ShadeIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource ShadeOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource Accent}'/>
                            <Setter Property='Foreground' Value='White'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Language chips: a CheckBox that looks like a selectable tile. -->
    <Style x:Key='Chip' TargetType='CheckBox'>
        <Setter Property='Foreground' Value='{StaticResource Muted}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRing}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='CheckBox'>
                    <Border x:Name='Root' Height='40' RenderTransformOrigin='0.5,0.5'>
                        <Border.RenderTransform><ScaleTransform/></Border.RenderTransform>
                        <Border x:Name='Bd' CornerRadius='10' Background='{StaticResource Control}'
                                BorderBrush='Transparent' BorderThickness='2'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource StrokeHover}'/>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource SinkIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource SinkOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentSoft}'/>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                            <Setter Property='Foreground' Value='{StaticResource Link}'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Capsule skins: one tile per look, the chosen one ringed in the accent. -->
    <Style x:Key='SkinTile' TargetType='RadioButton'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRing}'/>
        <Setter Property='Margin' Value='0,0,10,10'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RadioButton'>
                    <Border x:Name='Root' CornerRadius='12' Background='{StaticResource Control}' RenderTransformOrigin='0.5,0.5'>
                        <Border.RenderTransform><ScaleTransform/></Border.RenderTransform>
                        <Grid>
                            <Border x:Name='Bd' CornerRadius='12' BorderThickness='2' BorderBrush='Transparent'/>
                            <ContentPresenter Margin='12,14,12,10' HorizontalAlignment='Center'/>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource StrokeHover}'/>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource SinkIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource SinkOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Root' Property='Background' Value='{StaticResource AccentSoft}'/>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Languages: a tile per language; choosing one fills its tray in soft blue and
         pops a tick into the circle on its right. -->
    <Style x:Key='LanguageTile' TargetType='CheckBox'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusTile}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='CheckBox'>
                    <Border x:Name='Root' CornerRadius='12' Background='{StaticResource Control}' RenderTransformOrigin='0.5,0.5'>
                        <Border.RenderTransform><ScaleTransform/></Border.RenderTransform>
                        <Grid>
                            <Border x:Name='Bd' CornerRadius='12' BorderThickness='1.5' BorderBrush='Transparent'/>
                            <Border x:Name='Hover' CornerRadius='12' Background='{StaticResource Wash}' Opacity='0'/>
                            <DockPanel Margin='12,10,12,10'>
                                <Grid DockPanel.Dock='Right' Width='20' Height='20' Margin='10,0,0,0' VerticalAlignment='Center'>
                                    <Ellipse x:Name='Box' Stroke='{StaticResource StrokeHover}' StrokeThickness='1.5'/>
                                    <Grid x:Name='Tick' Opacity='0' RenderTransformOrigin='0.5,0.5'>
                                        <Grid.RenderTransform><ScaleTransform/></Grid.RenderTransform>
                                        <Ellipse Fill='{StaticResource Accent}'/>
                                        <TextBlock Text='&#xE73E;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets' FontSize='10'
                                                   Foreground='White' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                                    </Grid>
                                </Grid>
                                <ContentPresenter VerticalAlignment='Center'/>
                            </DockPanel>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource HoverIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource HoverOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Trigger.EnterActions><BeginStoryboard Storyboard='{StaticResource SinkIn}'/></Trigger.EnterActions>
                            <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource SinkOut}'/></Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Root' Property='Background' Value='{StaticResource AccentSoft}'/>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                            <Setter TargetName='Box' Property='Stroke' Value='{StaticResource Accent}'/>
                            <Setter TargetName='Tick' Property='Opacity' Value='1'/>
                        </Trigger>
                        <!-- Only a real check pops the tick in; a tile that opens checked just shows it. -->
                        <EventTrigger RoutedEvent='ToggleButton.Checked'>
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName='Tick' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                                     From='0.4' To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                    <DoubleAnimation Storyboard.TargetName='Tick' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                                     From='0.4' To='1' Duration='{StaticResource Settle}' EasingFunction='{StaticResource Out}'/>
                                </Storyboard>
                            </BeginStoryboard>
                        </EventTrigger>
                        <Trigger Property='IsEnabled' Value='False'>
                            <Setter Property='Opacity' Value='0.45'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Accent swatches: Background carries the color. -->
    <Style x:Key='Swatch' TargetType='RadioButton'>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusDot}'/>
        <Setter Property='Margin' Value='0,0,6,0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RadioButton'>
                    <Grid Width='28' Height='28' Background='Transparent'>
                        <Ellipse x:Name='Ring' Stroke='{StaticResource Text}' StrokeThickness='2' Opacity='0'/>
                        <Ellipse Margin='5' Fill='{TemplateBinding Background}'/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Ring' Property='Opacity' Value='0.3'/>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Ring' Property='Opacity' Value='1'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType='ComboBox'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusField}'/>
        <Setter Property='FontSize' Value='13'/>
        <Setter Property='Height' Value='34'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ComboBox'>
                    <Grid>
                        <ToggleButton Focusable='False' ClickMode='Press'
                                      IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
                            <ToggleButton.Template>
                                <ControlTemplate TargetType='ToggleButton'>
                                    <Border x:Name='Bd' CornerRadius='10' Background='{StaticResource Field}'
                                            BorderBrush='{StaticResource StrokeHover}' BorderThickness='1'>
                                        <TextBlock x:Name='Arrow' Text='&#xE70D;' FontFamily='Segoe Fluent Icons, Segoe MDL2 Assets'
                                                   FontSize='10' Foreground='{StaticResource Muted}'
                                                   HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'/>
                                    </Border>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property='IsMouseOver' Value='True'>
                                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Muted}'/>
                                            <Setter TargetName='Arrow' Property='Foreground' Value='{StaticResource Text}'/>
                                        </Trigger>
                                        <Trigger Property='IsChecked' Value='True'>
                                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </ToggleButton.Template>
                        </ToggleButton>
                        <ContentPresenter IsHitTestVisible='False' Margin='12,0,30,0'
                                          VerticalAlignment='Center' HorizontalAlignment='Left'
                                          Content='{TemplateBinding SelectionBoxItem}'
                                          ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'>
                            <ContentPresenter.Resources>
                                <Style TargetType='TextBlock'>
                                    <Setter Property='TextTrimming' Value='CharacterEllipsis'/>
                                </Style>
                            </ContentPresenter.Resources>
                        </ContentPresenter>
                        <Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True'
                               Focusable='False' PopupAnimation='Fade'>
                            <Grid Margin='10,4,10,14'>
                                <Border CornerRadius='12' Background='{StaticResource Raised}'>
                                    <Border.Effect>
                                        <DropShadowEffect BlurRadius='16' ShadowDepth='4' Direction='270' Opacity='0.16'/>
                                    </Border.Effect>
                                </Border>
                                <Border MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'
                                        Padding='4' CornerRadius='12' Background='{StaticResource Raised}'
                                        BorderBrush='{StaticResource Stroke}' BorderThickness='1'>
                                    <ScrollViewer VerticalScrollBarVisibility='Auto'>
                                        <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained'/>
                                    </ScrollViewer>
                                </Border>
                            </Grid>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType='ComboBoxItem'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Padding' Value='10,7'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ComboBoxItem'>
                    <Border x:Name='Bd' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='8'>
                        <ContentPresenter/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsHighlighted' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource Control}'/>
                        </Trigger>
                        <Trigger Property='IsSelected' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentSoft}'/>
                            <Setter Property='Foreground' Value='{StaticResource Link}'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType='ToolTip'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='FontSize' Value='12'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ToolTip'>
                    <Border Padding='10,6' CornerRadius='8' Background='{StaticResource Raised}'
                            BorderBrush='{StaticResource StrokeHover}' BorderThickness='1'>
                        <ContentPresenter/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Thin track + round thumb. -->
    <Style x:Key='SliderPart' TargetType='RepeatButton'>
        <Setter Property='Focusable' Value='False'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RepeatButton'>
                    <Border Height='4' CornerRadius='2' Background='{TemplateBinding Background}'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType='Slider'>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRing}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='Slider'>
                    <Track x:Name='PART_Track' Height='24'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Style='{StaticResource SliderPart}' Background='{StaticResource Accent}' Command='Slider.DecreaseLarge'/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Style='{StaticResource SliderPart}' Background='{StaticResource Control}' Command='Slider.IncreaseLarge'/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <!-- The knob grows while it's held, so the hand can feel it's got it. -->
                                    <ControlTemplate TargetType='Thumb'>
                                        <Grid Width='24' Height='24' Background='Transparent'>
                                            <Ellipse x:Name='Root' Width='16' Height='16' Fill='White' Stroke='{StaticResource Accent}' StrokeThickness='3'
                                                     RenderTransformOrigin='0.5,0.5'>
                                                <Ellipse.RenderTransform><ScaleTransform/></Ellipse.RenderTransform>
                                            </Ellipse>
                                        </Grid>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property='IsDragging' Value='True'>
                                                <Trigger.EnterActions>
                                                    <BeginStoryboard>
                                                        <Storyboard>
                                                            <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                                                             To='1.25' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
                                                            <DoubleAnimation Storyboard.TargetName='Root' Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                                                             To='1.25' Duration='{StaticResource Quick}' EasingFunction='{StaticResource Out}'/>
                                                        </Storyboard>
                                                    </BeginStoryboard>
                                                </Trigger.EnterActions>
                                                <Trigger.ExitActions><BeginStoryboard Storyboard='{StaticResource SinkOut}'/></Trigger.ExitActions>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType='ProgressBar'>
        <Setter Property='Height' Value='4'/>
        <Setter Property='Background' Value='{StaticResource Control}'/>
        <Setter Property='Foreground' Value='{StaticResource Accent}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ProgressBar'>
                    <Grid>
                        <Border x:Name='PART_Track' CornerRadius='2' Background='{TemplateBinding Background}'/>
                        <Border x:Name='PART_Indicator' CornerRadius='2' Background='{TemplateBinding Foreground}' HorizontalAlignment='Left'/>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- ponytail: vertical scroll bars only; no view here scrolls horizontally. -->
    <Style x:Key='Invisible' TargetType='RepeatButton'>
        <Setter Property='Focusable' Value='False'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RepeatButton'>
                    <Border Background='Transparent'/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType='ScrollBar'>
        <Setter Property='Width' Value='6'/>
        <Setter Property='MinWidth' Value='6'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ScrollBar'>
                    <Track x:Name='PART_Track' IsDirectionReversed='True'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Style='{StaticResource Invisible}' Command='ScrollBar.PageUpCommand'/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Style='{StaticResource Invisible}' Command='ScrollBar.PageDownCommand'/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Border x:Name='T' CornerRadius='3' Background='{StaticResource StrokeHover}'/>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property='IsMouseOver' Value='True'>
                                                <Setter TargetName='T' Property='Background' Value='{StaticResource Muted}'/>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";
}
