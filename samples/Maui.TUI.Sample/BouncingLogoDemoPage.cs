using System.Text;
using Maui.TUI.Animation;
using Maui.TUI.Controls;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Maui.TUI.Sample;

/// <summary>
/// A bouncing rectangle animation inspired by the classic DVD screensaver
/// from that famous episode of "The Office" — will it hit the corner?
/// Demonstrates <see cref="AsciiCanvasView"/> with time-based animation,
/// color transitions on wall bounces, and CellBuffer drawing primitives.
/// </summary>
class BouncingLogoDemoPage : ContentPage
{
	// ── Canvas dimensions ──────────────────────────
	const int CanvasW = 60;
	const int CanvasH = 20;

	// ── Logo (the bouncing rectangle) ──────────────
	const int LogoW = 7;
	const int LogoH = 3;
	static readonly string[] LogoLines = ["┌─DVD─┐", "│ TUI │", "└─────┘"];

	// ── Movement ───────────────────────────────────
	// Pixels (cells) per second on each axis
	const double SpeedX = 12.0;
	const double SpeedY = 6.0;

	// ── Color palette — cycles on each bounce ──────
	static readonly Color[] Palette =
	[
		Colors.Red,
		Colors.Orange,
		Colors.Yellow,
		Colors.Lime,
		Colors.Cyan,
		Colors.DeepSkyBlue,
		Colors.MediumPurple,
		Colors.HotPink,
	];

	// ── State ──────────────────────────────────────
	double _x = 2, _y = 1;
	double _dx = 1, _dy = 1;       // direction multipliers (+1 or -1)
	int _colorIndex;
	int _nextColorIndex = 1;
	double _colorLerp = 1.0;       // 1.0 = fully at current color
	const double FadeDuration = 0.4; // seconds to fade between colors
	int _bounceCount;
	int _cornerHits;

	readonly Label _statusLabel;

	public BouncingLogoDemoPage()
	{
		Title = "Animation";

		_statusLabel = new Label { Text = "Bounces: 0 | Corner hits: 0" };

		var canvas = new AsciiCanvasView
		{
			CanvasWidth = CanvasW,
			CanvasHeight = CanvasH,
			TargetFps = 30,
			ClearBeforeDraw = true,
		};
		canvas.DrawFrame += OnDrawFrame;

		var startButton = new Button { Text = "Start" };
		var stopButton = new Button { Text = "Stop" };

		startButton.Clicked += (s, e) => canvas.StartAnimation();
		stopButton.Clicked += (s, e) => canvas.StopAnimation();

		Content = new VerticalStackLayout
		{
			Spacing = 1,
			Children =
			{
				new Label { Text = "DVD Screensaver — will it hit the corner?" },
				canvas,
				new HorizontalStackLayout
				{
					Spacing = 2,
					Children = { startButton, stopButton },
				},
				_statusLabel,
			}
		};

		// Auto-start
		canvas.StartAnimation();
	}

	void OnDrawFrame(object? sender, AsciiCanvasDrawEventArgs e)
	{
		double dt = e.DeltaTime.TotalSeconds;
		if (dt <= 0 && e.FrameNumber > 0)
			return; // skip degenerate frames

		// ── Move ───────────────────────────────────
		_x += _dx * SpeedX * dt;
		_y += _dy * SpeedY * dt;

		// ── Bounce ─────────────────────────────────
		bool bounced = false;
		bool cornerHit = false;
		bool hitLeft = false, hitRight = false, hitTop = false, hitBottom = false;

		if (_x <= 0)
		{
			_x = 0;
			_dx = 1;
			bounced = true;
			hitLeft = true;
		}
		else if (_x >= CanvasW - LogoW)
		{
			_x = CanvasW - LogoW;
			_dx = -1;
			bounced = true;
			hitRight = true;
		}

		if (_y <= 0)
		{
			_y = 0;
			_dy = 1;
			bounced = true;
			hitTop = true;
		}
		else if (_y >= CanvasH - LogoH)
		{
			_y = CanvasH - LogoH;
			_dy = -1;
			bounced = true;
			hitBottom = true;
		}

		// Corner hit = simultaneous horizontal + vertical bounce
		if ((hitLeft || hitRight) && (hitTop || hitBottom))
		{
			cornerHit = true;
			_cornerHits++;
		}

		if (bounced)
		{
			_bounceCount++;
			// Start color fade to next palette entry
			_colorIndex = _nextColorIndex;
			_nextColorIndex = (_nextColorIndex + 1) % Palette.Length;
			_colorLerp = 0.0;
		}

		// ── Advance color fade ─────────────────────
		if (_colorLerp < 1.0)
		{
			_colorLerp += dt / FadeDuration;
			if (_colorLerp > 1.0)
				_colorLerp = 1.0;
		}

		Color logoColor = LerpColor(Palette[_colorIndex], Palette[_nextColorIndex], (float)_colorLerp);

		// ── Draw border ────────────────────────────
		DrawBorder(e.Buffer);

		// ── Draw logo ──────────────────────────────
		int drawX = (int)Math.Round(_x);
		int drawY = (int)Math.Round(_y);

		for (int row = 0; row < LogoLines.Length; row++)
		{
			e.Buffer.DrawString(
				drawX, drawY + row,
				LogoLines[row],
				logoColor, Colors.Black,
				CellAttributes.Bold);
		}

		// ── Corner hit flash ───────────────────────
		if (cornerHit)
		{
			// Flash all four corners to celebrate
			e.Buffer.SetCell(0, 0, new Rune('*'), Colors.White, Colors.Red, CellAttributes.Bold | CellAttributes.Blink);
			e.Buffer.SetCell(CanvasW - 1, 0, new Rune('*'), Colors.White, Colors.Red, CellAttributes.Bold | CellAttributes.Blink);
			e.Buffer.SetCell(0, CanvasH - 1, new Rune('*'), Colors.White, Colors.Red, CellAttributes.Bold | CellAttributes.Blink);
			e.Buffer.SetCell(CanvasW - 1, CanvasH - 1, new Rune('*'), Colors.White, Colors.Red, CellAttributes.Bold | CellAttributes.Blink);
		}

		// ── Update status (only when changed to avoid alloc every frame) ──
		if (bounced)
		{
			_statusLabel.Text = cornerHit
				? $"Bounces: {_bounceCount} | Corner hits: {_cornerHits} | CORNER HIT!!!"
				: $"Bounces: {_bounceCount} | Corner hits: {_cornerHits}";
		}
	}

	/// <summary>
	/// Draws a thin border around the canvas edges using box-drawing characters.
	/// </summary>
	static void DrawBorder(CellBuffer buffer)
	{
		int w = buffer.Width;
		int h = buffer.Height;
		var borderColor = new Color(0.3f, 0.3f, 0.3f); // dim gray

		// Top and bottom edges
		for (int col = 1; col < w - 1; col++)
		{
			buffer.SetCell(col, 0, new Rune('─'), borderColor, Colors.Black);
			buffer.SetCell(col, h - 1, new Rune('─'), borderColor, Colors.Black);
		}

		// Left and right edges
		for (int row = 1; row < h - 1; row++)
		{
			buffer.SetCell(0, row, new Rune('│'), borderColor, Colors.Black);
			buffer.SetCell(w - 1, row, new Rune('│'), borderColor, Colors.Black);
		}

		// Corners
		buffer.SetCell(0, 0, new Rune('┌'), borderColor, Colors.Black);
		buffer.SetCell(w - 1, 0, new Rune('┐'), borderColor, Colors.Black);
		buffer.SetCell(0, h - 1, new Rune('└'), borderColor, Colors.Black);
		buffer.SetCell(w - 1, h - 1, new Rune('┘'), borderColor, Colors.Black);
	}

	/// <summary>
	/// Linearly interpolates between two MAUI colors.
	/// </summary>
	static Color LerpColor(Color a, Color b, float t)
	{
		return new Color(
			a.Red + (b.Red - a.Red) * t,
			a.Green + (b.Green - a.Green) * t,
			a.Blue + (b.Blue - a.Blue) * t);
	}
}
