﻿using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using MediatR;
using PacMan.GameComponents.Audio;
using PacMan.GameComponents.Canvas;
using PacMan.GameComponents.Events;

namespace PacMan.GameComponents.GameActs
{
    public class TornGhostChaseAct : IAct
    {
        readonly IMediator _mediator;
        readonly IGameSoundPlayer _gameSoundPlayer;
        readonly AttractScenePacMan _pacMan;
        readonly GeneralSprite _worm;

        readonly GeneralSprite _blinky;

        readonly StartAndEndPos _pacPositions;
        readonly EggTimer _pacTimer;

        StartAndEndPos _ghostStartAndEndPos;
        EggTimer _ghostTimer;

        bool _finished;

        [SuppressMessage("ReSharper", "HeapView.ObjectAllocation.Evident")]
        public TornGhostChaseAct(IMediator mediator, IGameSoundPlayer gameSoundPlayer)
        {
            _mediator = mediator;
            _gameSoundPlayer = gameSoundPlayer;
            _finished = false;

            var justOffScreen = new Vector2(250, 140);

            _ghostTimer = new EggTimer(4500.Milliseconds(), reverseChase);

            _pacTimer = new EggTimer(4800.Milliseconds(), () => { });

            _pacMan = new AttractScenePacMan
            {
                Direction = Directions.Left
            };

            _worm = new GeneralSprite(Vector2.Zero,
                new Size(22, 11),
                new Vector2(11, 5.5f),
                new Vector2(594, 132),
                new Vector2(626, 132),
                110.Milliseconds())
            {
                Visible = false
            };


            _blinky = new GeneralSprite(Vector2.Zero,
                new Size(14, 14),
                new Vector2(7.5f, 7.5f),
                new Vector2(618, 113),
                new Vector2(634, 113),
                110.Milliseconds());


            _pacPositions = new StartAndEndPos(justOffScreen, new Vector2(-70, justOffScreen.Y));

            _ghostStartAndEndPos = new StartAndEndPos(justOffScreen + new Vector2(50, 0), new Vector2(-40, justOffScreen.Y));
        }

        public string Name { get; } = "TornGhostChaseAct";

        public async ValueTask Reset()
        {
            _finished = false;
            _pacMan.Position = _pacPositions.Start;
            _blinky.Position = _ghostStartAndEndPos.Start;

            await _gameSoundPlayer.CutScene();
        }

        public async ValueTask<ActUpdateResult> Update(CanvasTimingInformation gameContext)
        {
            _ghostTimer.Run(gameContext);
            _pacTimer.Run(gameContext);
            await _worm.Update(gameContext);

            lerpBlinky();
            lerpPacMan();

            await _pacMan.Update(gameContext);
            await _blinky.Update(gameContext);
            await _worm.Update(gameContext);

            return _finished ? ActUpdateResult.Finished : ActUpdateResult.Running;
        }

        public async ValueTask Draw(CanvasWrapper canvas)
        {
            await _blinky.Draw(canvas);
            await _pacMan.Draw(canvas);
            await _worm.Draw(canvas);
        }

        void lerpBlinky()
        {
            var pc = _ghostTimer.Progress;
            _blinky.Position = Vector2.Lerp(_ghostStartAndEndPos.Start, _ghostStartAndEndPos.End, pc);
            _worm.Position = Vector2.Lerp(_ghostStartAndEndPos.Start, _ghostStartAndEndPos.End, pc);
        }

        void lerpPacMan()
        {
            var pc = _pacTimer.Progress;

            _pacMan.Position = Vector2.Lerp(_pacPositions.Start, _pacPositions.End, pc);
        }

        void reverseChase()
        {
            _ghostTimer = new EggTimer(4600.Milliseconds(), async () =>
            {
                _finished = true; 
                await _mediator.Publish(new CutSceneFinishedEvent());
            });

            _pacMan.Visible = false;
            _blinky.Visible = false;
            _worm.Visible = true;

            _ghostStartAndEndPos = _ghostStartAndEndPos.Reverse();
        }
    }
}