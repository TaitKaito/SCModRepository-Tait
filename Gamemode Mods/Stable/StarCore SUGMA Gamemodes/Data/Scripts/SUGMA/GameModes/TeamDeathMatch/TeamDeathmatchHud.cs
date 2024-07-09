﻿using System;
using System.Collections.Generic;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using SC.SUGMA.GameState;
using VRage.Game.ModAPI;
using VRageMath;

namespace SC.SUGMA.GameModes.TeamDeathMatch
{
    internal class TeamDeathmatchHud : ComponentBase
    {
        private const int MatchResultsVisibleTicks = 900;
        private int _closeTime = -1;
        private readonly TeamDeathmatchGamemode _gamemode;

        public TDMHud_Window Window;

        public TeamDeathmatchHud(TeamDeathmatchGamemode gamemode)
        {
            _gamemode = gamemode;
        }

        public override void Init(string id)
        {
            base.Init(id);

            if (!RichHudClient.Registered)
                throw new Exception("RichHudAPI was not initialized in time!");

            Window = new TDMHud_Window(HudMain.HighDpiRoot, _gamemode);
        }

        public override void Close()
        {
            HudMain.HighDpiRoot.RemoveChild(Window);
        }

        public override void UpdateTick()
        {
            Window.Update();
            if (_closeTime > 0)
                _closeTime--;

            if (_closeTime == 0) SUGMA_SessionComponent.I.UnregisterComponent(ComponentId);
        }

        public void MatchEnded(IMyFaction winner)
        {
            Window.MatchEnded(winner);
            _closeTime = MatchResultsVisibleTicks;
        }
    }

    internal class TDMHud_Window : HudElementBase
    {
        private readonly TeamDeathmatchGamemode _gamemode;

        private bool _matchEnded;
        private readonly MatchTimer _timer;

        private readonly LabelBox _timerLabel;
        public TDMHud_TeamBanner[] Banners;

        public TDMHud_Window(HudParentBase parent, TeamDeathmatchGamemode gamemode) : base(parent)
        {
            _gamemode = gamemode;
            _timer = gamemode._matchTimer;

            if (_gamemode == null)
                throw new Exception("Null TDM gamemode!");
            if (_timer == null)
                throw new Exception("Null match timer!");

            Size = new Vector2(640, TDMHud_TeamBanner.BaseHeight);

            Offset = new Vector2(0, 515); // Regardless of screen size, this is out of 1920x1080

            _timerLabel = new LabelBox(this)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top,
                Height = TDMHud_TeamBanner.BaseHeight,
                DimAlignment = DimAlignments.Height,
                Text = "20:00",
                TextPadding = new Vector2(2.5f, 0),
                FitToTextElement = false,
                Color = new Color(255, 255, 255, 40)
            };

            var banners = new List<TDMHud_TeamBanner>(_gamemode.TrackedFactions.Count);
            var idx = 0;
            foreach (var faction in _gamemode.TrackedFactions)
            {
                var newBanner = new TDMHud_TeamBanner(this, faction.Key, faction.Value, idx % 2 == 0)
                {
                    ParentAlignment =
                        ParentAlignments.Inner |
                        (idx % 2 == 0 ? ParentAlignments.Left : ParentAlignments.Right) |
                        ParentAlignments.Top,

                    Offset = new Vector2(0, (int)-Math.Floor(idx / 2f) * (TDMHud_TeamBanner.BaseHeight + 5))
                };
                banners.Add(newBanner);
                idx++;
            }

            Banners = banners.ToArray();
        }

        public void Update()
        {
            if (_matchEnded)
                return;

            var matchTime = _timer.CurrentMatchTime;
            var matchSeconds = (int)matchTime.TotalSeconds;
            var basePoints = (int)(_timer.MatchDurationMinutes * 60);

            var remainingMinutes = (int)Math.Floor(_timer.MatchDurationMinutes - matchTime.TotalMinutes);
            var remainingSeconds =
                (int)((_timer.MatchDurationMinutes - matchTime.TotalMinutes - remainingMinutes) * 60);

            _timerLabel.Text =
                $"{(remainingMinutes < 10 ? "0" + remainingMinutes : remainingMinutes.ToString())}:{(remainingSeconds < 10 ? "0" + remainingSeconds : remainingSeconds.ToString())}";

            foreach (var banner in Banners) banner.Update(_gamemode.CalculateFactionPoints(banner.Faction), basePoints);
        }

        public void MatchEnded(IMyFaction winner)
        {
            _matchEnded = true;
            var winnerPoints = 0;
            foreach (var banner in Banners)
            {
                if (banner.Faction == winner)
                    winnerPoints =
                        (int)(_timer.MatchDurationMinutes * 60 * (_gamemode.PointTracker.GetFactionPoints(winner) /
                                                                  (float)banner.StartShipCount) -
                              _timer.CurrentMatchTime.TotalSeconds);
                RemoveChild(banner);
            }

            if (_timerLabel == null)
                return;

            var winnerLabel = new LabelBox(_timerLabel)
            {
                Text = winner != null
                    ? $"A WINNER IS {winner.Name}. {winnerPoints} tickets remaining."
                    : "YOU ARE ALL LOSERS",
                ParentAlignment = ParentAlignments.Bottom,
                Height = TDMHud_TeamBanner.BaseHeight,
                TextPadding = new Vector2(2.5f, 0),
                Color = new Color(255, 255, 255, 40)
            };

            winnerLabel.TextBoard.SetFormatting(GlyphFormat.White.WithColor(Color.Red).WithSize(3)
                .WithAlignment(TextAlignment.Center));
        }
    }
}