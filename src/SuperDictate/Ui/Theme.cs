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
    /// Telegram day (light) and night (dark) palettes. Telegram's own #3390EC is
    /// deepened to #1E77CE so white text on it meets WCAG AA; every text pair
    /// meets 4.5:1 and every control outline 3:1, which the self-test checks.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Palette = new Dictionary<string, string>
    {
        // The field the cards sit on, and the sidebar.
        ["Bg"] = "#0E1621",
        ["Side"] = "#17212B",
        ["Card"] = "#17212B",
        ["Field"] = "#242F3D",
        // Hover fill, slider and progress tracks.
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
        // Hover and press overlay on buttons.
        ["Wash"] = "#FFFFFF",
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

    public static ResourceDictionary Create() => (ResourceDictionary)XamlReader.Parse(Xaml.Replace(
        "<!-- palette -->",
        string.Join("\n    ", Palette.Select(pair => $"<SolidColorBrush x:Key='{pair.Key}' Color='{pair.Value}'/>"))));

    private const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <!-- palette -->

    <!-- Buttons are soft blue with blue text; one solid blue button per job (AccentButton).
         A Wash overlay gives hover/press feedback; an outer ring shows keyboard focus. -->
    <Style TargetType='Button'>
        <Setter Property='Foreground' Value='{StaticResource Link}'/>
        <Setter Property='Background' Value='{StaticResource AccentSoft}'/>
        <Setter Property='BorderBrush' Value='Transparent'/>
        <Setter Property='Height' Value='32'/>
        <Setter Property='Padding' Value='14,0'/>
        <Setter Property='FontSize' Value='13'/>
        <Setter Property='FontWeight' Value='SemiBold'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='Button'>
                    <Grid>
                        <Border x:Name='Ring' Margin='-3' CornerRadius='13' BorderThickness='2'
                                BorderBrush='{StaticResource Accent}' Visibility='Hidden'/>
                        <Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                                BorderThickness='1' CornerRadius='10' SnapsToDevicePixels='True'>
                            <Grid>
                                <Border x:Name='Wash' Background='{StaticResource Wash}' Opacity='0' CornerRadius='9'/>
                                <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                            </Grid>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Wash' Property='Opacity' Value='0.05'/>
                        </Trigger>
                        <Trigger Property='IsPressed' Value='True'>
                            <Setter TargetName='Wash' Property='Opacity' Value='0.11'/>
                        </Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Ring' Property='Visibility' Value='Visible'/>
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
        <Setter Property='Width' Value='32'/>
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

    <!-- Boolean settings are Telegram switches: the knob slides over in 160 ms. -->
    <Style TargetType='CheckBox'>
        <Setter Property='Foreground' Value='{StaticResource Text}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='CheckBox'>
                    <StackPanel Orientation='Horizontal' Background='Transparent'>
                        <Grid VerticalAlignment='Center'>
                            <Border x:Name='Ring' Margin='-3' CornerRadius='14' BorderThickness='2'
                                    BorderBrush='{StaticResource Accent}' Visibility='Hidden'/>
                            <Border x:Name='Track' Width='40' Height='22' CornerRadius='11'
                                    Background='{StaticResource Control}' BorderBrush='{StaticResource Muted}' BorderThickness='1.5'>
                                <Ellipse x:Name='Knob' Width='14' Height='14' Margin='3,0,0,0' HorizontalAlignment='Left'
                                         Fill='{StaticResource Muted}'/>
                            </Border>
                        </Grid>
                        <ContentPresenter x:Name='Label' Margin='10,0,0,0' VerticalAlignment='Center'/>
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property='HasContent' Value='False'>
                            <Setter TargetName='Label' Property='Visibility' Value='Collapsed'/>
                        </Trigger>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Knob' Property='Fill' Value='{StaticResource Text}'/>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Track' Property='Background' Value='{StaticResource Accent}'/>
                            <Setter TargetName='Track' Property='BorderBrush' Value='{StaticResource Accent}'/>
                            <Setter TargetName='Knob' Property='Fill' Value='White'/>
                            <!-- The resting position, so a switch that opens checked doesn't slide in. -->
                            <Setter TargetName='Knob' Property='Margin' Value='20,0,0,0'/>
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <ThicknessAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='Margin'
                                                            To='20,0,0,0' Duration='0:0:0.16'>
                                            <ThicknessAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></ThicknessAnimation.EasingFunction>
                                        </ThicknessAnimation>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                            <Trigger.ExitActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <ThicknessAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='Margin'
                                                            To='3,0,0,0' Duration='0:0:0.16'>
                                            <ThicknessAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></ThicknessAnimation.EasingFunction>
                                        </ThicknessAnimation>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.ExitActions>
                        </Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Ring' Property='Visibility' Value='Visible'/>
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
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Margin' Value='0,2'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RadioButton'>
                    <Border x:Name='Bd' Height='42' CornerRadius='10' Background='Transparent'
                            BorderBrush='Transparent' BorderThickness='2'>
                        <ContentPresenter Margin='10,0' VerticalAlignment='Center'
                                          HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource Control}'/>
                        </Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource Accent}'/>
                            <Setter Property='Foreground' Value='White'/>
                        </Trigger>
                        <MultiTrigger>
                            <MultiTrigger.Conditions>
                                <Condition Property='IsChecked' Value='True'/>
                                <Condition Property='IsKeyboardFocused' Value='True'/>
                            </MultiTrigger.Conditions>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='White'/>
                        </MultiTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Language chips: a CheckBox that looks like a selectable tile. -->
    <Style x:Key='Chip' TargetType='CheckBox'>
        <Setter Property='Foreground' Value='{StaticResource Muted}'/>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='CheckBox'>
                    <Border x:Name='Bd' Height='40' CornerRadius='10' Background='{StaticResource Control}'
                            BorderBrush='Transparent' BorderThickness='2'>
                        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource StrokeHover}'/>
                        </Trigger>
                        <Trigger Property='IsChecked' Value='True'>
                            <Setter TargetName='Bd' Property='Background' Value='{StaticResource AccentSoft}'/>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Accent}'/>
                            <Setter Property='Foreground' Value='{StaticResource Link}'/>
                        </Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Bd' Property='BorderBrush' Value='{StaticResource Text}'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Accent swatches: Background carries the color. -->
    <Style x:Key='Swatch' TargetType='RadioButton'>
        <Setter Property='Cursor' Value='Hand'/>
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
        <Setter Property='Margin' Value='0,0,6,0'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='RadioButton'>
                    <Grid Width='28' Height='28' Background='Transparent'>
                        <Ellipse x:Name='Focus' Margin='-3' Stroke='{StaticResource Accent}' StrokeThickness='2' Visibility='Hidden'/>
                        <Ellipse x:Name='Ring' Stroke='{StaticResource Text}' StrokeThickness='2' Opacity='0'/>
                        <Ellipse Margin='5' Fill='{TemplateBinding Background}'/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'>
                            <Setter TargetName='Ring' Property='Opacity' Value='0.3'/>
                        </Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Focus' Property='Visibility' Value='Visible'/>
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
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
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
                        <Border x:Name='FocusBd' CornerRadius='10' BorderThickness='2' BorderBrush='{StaticResource Accent}'
                                IsHitTestVisible='False' Visibility='Hidden'/>
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
                        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
                            <Setter TargetName='FocusBd' Property='Visibility' Value='Visible'/>
                        </Trigger>
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
        <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
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
                            <Thumb x:Name='Thumb' BorderBrush='Transparent'>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Grid Width='24' Height='24'>
                                            <Ellipse Stroke='{TemplateBinding BorderBrush}' StrokeThickness='2'/>
                                            <Ellipse Width='16' Height='16' Fill='White' Stroke='{StaticResource Accent}' StrokeThickness='3'/>
                                        </Grid>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsKeyboardFocused' Value='True'>
                            <Setter TargetName='Thumb' Property='BorderBrush' Value='{StaticResource Accent}'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
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
