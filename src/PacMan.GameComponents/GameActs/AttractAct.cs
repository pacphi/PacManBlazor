﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using MediatR;
using PacMan.GameComponents.Audio;
using PacMan.GameComponents.Canvas;
using PacMan.GameComponents.Events;
using PacMan.GameComponents.Ghosts;

namespace PacMan.GameComponents.GameActs
{
    /// Show's the attract screen (ghost names and pictures and the 'chase' sub act).  Transitions to either
    /// the 'player intro' act (to start the demo mode if nothing's was pressed/clicked/touched),
    /// or the 'start button' act if a coin was 'inserted'.
    public class AttractAct : IAct
    {
        readonly ICoinBox _coinBox;
        readonly IMediator _mediator;
        readonly IHumanInterfaceParser _input;
        readonly IGameSoundPlayer _gameSoundPlayer;
        readonly Marquee _marquee;
        readonly GeneralSprite _pacmanLogo;

        struct Instruction
        {
            public TimeSpan When;
            public SimpleGhost Ghost;
            public string Text;
            public Vector2 Where;
            public Color Color;
        }

        ChaseSubAct _chaseSubAct;
        readonly SimpleGhost _blinky;
        readonly SimpleGhost _pinky;
        readonly SimpleGhost _inky;
        readonly SimpleGhost _clyde;

        readonly List<Instruction> _instructions;

        //IAct _nextAct;

        TimeSpan _startTime;
        TimeSpan _drawUpTo;
        readonly TimeSpan _chaseSubActReadyAt;
        bool _chaseSubActReady;

        readonly object _lock;
        bool _finished;
        readonly BlazorLogo _blazorLogo;

        [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
        public AttractAct(
            ICoinBox coinBox,
            IMediator mediator,
            IHumanInterfaceParser input,
            IGameSoundPlayer gameSoundPlayer)
        {
            MarqueeText[] texts = 
            {
                new MarqueeText
                {
                    Text = "tap or space for 1 player",
                    YPosition = 195,
                    TimeIdle = 1.Seconds(),
                    TimeIn = 2.Seconds(),
                    TimeStationary = 2.Seconds(),
                    TimeOut = 1.Seconds()
                },
                new MarqueeText
                {
                    Text = "press or 2 for 2 players",
                    YPosition = 195,
                    TimeIdle = 0.Seconds(),
                    TimeIn = 2.Seconds(),
                    TimeStationary = 2.Seconds(),
                    TimeOut = 1.Seconds()
                }
            };

            _marquee = new Marquee(texts);
            _coinBox = coinBox;
            _mediator = mediator;
            _input = input;
            _gameSoundPlayer = gameSoundPlayer;
            _pacmanLogo =
                new GeneralSprite(new Vector2(192, 25), new Size(36, 152), Vector2.Zero, new Vector2(456, 173));
            _blazorLogo = new BlazorLogo();

            _instructions = new List<Instruction>();

            _startTime = TimeSpan.MinValue;

            _blinky = new SimpleGhost(GhostNickname.Blinky, Directions.Right);
            _pinky = new SimpleGhost(GhostNickname.Pinky, Directions.Right);
            _inky = new SimpleGhost(GhostNickname.Inky, Directions.Right);
            _clyde = new SimpleGhost(GhostNickname.Clyde, Directions.Right);
            _startTime = TimeSpan.MinValue;
            _chaseSubActReadyAt = 9.Seconds();

            _chaseSubAct = new ChaseSubAct();
            _lock = new object();
        }

        public string Name { get; } = "AttractAct";

        public ValueTask Reset()
        {
            _startTime = TimeSpan.MinValue;
            _finished = false;
            _instructions.Clear();
            _chaseSubAct = new ChaseSubAct();
            populateDelayedInstructions();

            return default;
        }

        public async ValueTask<ActUpdateResult> Update(CanvasTimingInformation timing)
        {
            await _blazorLogo.Update(timing);

            await _marquee.Update(timing);

            if (_startTime == TimeSpan.MinValue)
            {
                _startTime = timing.TotalTime;
            }

            _drawUpTo = timing.TotalTime - _startTime;

            if (_input.WasKeyPressedAndReleased(Keys.Left))
            {
                await startDemoGame();
                return ActUpdateResult.Running;
            }

            if (_input.WasKeyPressedAndReleased(Keys.Five))
            {
                await _gameSoundPlayer.Enable();
                await _mediator.Publish(new CoinInsertedEvent());

                return ActUpdateResult.Running;
            }
            
            if (_input.WasKeyPressedAndReleased(Keys.Space) || 
                _input.WasKeyPressedAndReleased(Keys.One) ||
                _input.WasTapped)
            {
                await _gameSoundPlayer.Enable(); 
                _coinBox.CoinInserted();
                await _mediator.Publish(new NewGameEvent(1));

                return ActUpdateResult.Running;
            }
            if (_input.WasKeyPressedAndReleased(Keys.Two) ||_input.WasLongPress)
            {
                _coinBox.CoinInserted();

                _coinBox.CoinInserted();
                _coinBox.CoinInserted();

                await _mediator.Publish(new NewGameEvent(2));

                return ActUpdateResult.Running;
            }

            _chaseSubActReady = timing.TotalTime - _startTime >= _chaseSubActReadyAt;

            if (_chaseSubActReady)
            {
                if (await _chaseSubAct.Update(timing) == ActUpdateResult.Finished)
                {
                    if (!_finished)
                    {
                        await startDemoGame();
                        _finished = true;
                    }

                    return ActUpdateResult.Finished;
                }
            }

            return ActUpdateResult.Running;
        }

        public async ValueTask Draw(CanvasWrapper session)
        {
            for (int i = 0; i < _instructions.Count; i++)
            {
                var inst = _instructions[i];

                if (inst.When > _drawUpTo)
                {
                    break;
                }

                var ghost = inst.Ghost;

                if (ghost != null)
                {
                    ghost.Position = inst.Where;

                    await session.DrawSprite(ghost, Spritesheet.Reference);
                }
                else
                {
                    await drawText(session, inst.Text, inst.Where, inst.Color);
                }
            }

            if (_chaseSubActReady)
            {
                await _chaseSubAct.Draw(session);
            }

            await _marquee.Draw(session);


            await session.SetGlobalAlphaAsync(.75f);
            await session.DrawSprite(_pacmanLogo, Spritesheet.Reference);
            await session.SetGlobalAlphaAsync(1f);

            await _blazorLogo.Draw(session);
        }

        void populateDelayedInstructions()
        {
            lock (_lock)
            {
                TimeSpan clock = 1500.Milliseconds();


                _instructions.Add(new Instruction
                {
                    When = clock,
                    Where = new Vector2(32, 12),
                    Text = "CHARACTER / NICKNAME",
                    Color = Color.White
                });

                var gap = new Vector2(0, 24);

                var pos = new Vector2(16, 30);

                var timeForEachOne = 600.Milliseconds();

                writeInstructionsForGhost(ref clock, _blinky, Colors.Red, "SHADOW", "BLINKY", pos);

                clock += timeForEachOne;
                pos += gap;
                writeInstructionsForGhost(ref clock, _pinky, Colors.Pink, "SPEEDY", "PINKY", pos);

                clock += timeForEachOne;
                pos += gap;
                writeInstructionsForGhost(ref clock, _inky, Colors.Cyan, "BASHFUL", "INKY", pos);

                clock += timeForEachOne;
                pos += gap;
                writeInstructionsForGhost(ref clock, _clyde, Colors.Yellow, "POKEY", "CLYDE", pos);
            }
        }

        static ValueTask drawText(CanvasWrapper canvasWrapper, string text, Vector2 point, Color color)
        {
            return canvasWrapper.DrawMyText(text, point, color);
        }

        void writeInstructionsForGhost(ref TimeSpan clock,
            SimpleGhost ghost,
            Color color,
            string name,
            string nickname,
            Vector2 point)
        {
            _instructions.Add(new Instruction
            {
                Ghost = ghost,
                When = clock,
                Where = point
            });

            point += new Vector2(18, -4);

            clock += 1.Seconds();

            _instructions.Add(new Instruction
            {
                Where = point,
                Text = $@" - {name}",
                When = clock,
                Color = color
            });

            point += new Vector2(90, 0);

            clock += 500.Milliseconds();

            _instructions.Add(new Instruction
            {
                Where = point,
                Text = $@"""{nickname}""",
                When = clock,
                Color = color
            });
        }

        async ValueTask startDemoGame() => await _mediator.Publish(new DemoStartedEvent());
    }
}