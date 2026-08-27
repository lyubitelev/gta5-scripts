using System;
using System.IO;
using System.Media;
using GTA;
using GTA.Math;
using GTA.Native;
using gta.Core;

namespace gta.Player
{
    internal sealed class BongService
    {
        private enum BongState
        {
            Idle,
            LoadingAssets,
            Smoking,
            LoadingDrunkClipset,
            Drunk,
            Cleanup
        }

        private const string BongModelName = "prop_bong_01";
        private const string LighterModelName = "p_cs_lighter_01";
        private const string AnimDict = "anim@safehouse@bong";
        private const string AnimName = "bong_stage3";
        private const string DrunkClipset = "move_m@drunk@slightlydrunk";
        private const string ScreenEffect = "DrugsDrivingIn";
        private const string FacialMood = "mood_drunk_1";

        private BongState _state = BongState.Idle;
        private int _stateTimer;

        private Model _bongModel;
        private Model _lighterModel;
        private Prop _bongProp;
        private Prop _lighterProp;
        private SoundPlayer _soundPlayer;

        public void Start()
        {
            if (_state != BongState.Idle)
            {
                Notifier.Show("Вы уже заняты этим процессом");
                return;
            }

            Ped player = Game.Player.Character;
            if (player == null || !player.Exists() || player.IsInVehicle())
            {
                Notifier.Show("Нельзя использовать в транспорте");
                return;
            }

            _bongModel = new Model(BongModelName);
            _lighterModel = new Model(LighterModelName);

            _bongModel.Request();
            _lighterModel.Request();

            Function.Call(Hash.REQUEST_ANIM_DICT, AnimDict);
            Function.Call(Hash.REQUEST_NAMED_PTFX_ASSET, "core");

            _state = BongState.LoadingAssets;
            ModLogger.Log("BONG", "Started loading assets");
        }

        public void Update()
        {
            Ped player = Game.Player.Character;
            if (player == null || !player.Exists())
            {
                return;
            }

            switch (_state)
            {
                case BongState.LoadingAssets:
                    if (_bongModel.IsLoaded && _lighterModel.IsLoaded &&
                        Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, AnimDict) &&
                        Function.Call<bool>(Hash.HAS_NAMED_PTFX_ASSET_LOADED, "core"))
                    {
                        ModLogger.Log("BONG", "Assets loaded, starting animation");
                        
                        player.Task.ClearAllImmediately();
                        player.Task.PlayAnimation(AnimDict, AnimName, 8f, -8f, -1, (AnimationFlags)2, 0.0f);

                        // Спавним пропы и крепим к рукам
                        _bongProp = World.CreateProp(_bongModel, player.Position, false, false);
                        _lighterProp = World.CreateProp(_lighterModel, player.Position, false, false);

                        if (_bongProp != null && _bongProp.Exists())
                        {
                            _bongProp.AttachTo(player.Bones[(Bone)18905], new Vector3(0.07f, -0.21f, 0.1f), new Vector3(-108.81f, 8.3f, 0.0f));
                        }

                        if (_lighterProp != null && _lighterProp.Exists())
                        {
                            _lighterProp.AttachTo(player.Bones[(Bone)57005], new Vector3(0.11f, 0.03f, -0.01f), new Vector3(-86f, 11.73f, 0.0f));
                        }

                        // Играем звук
                        string soundPath = @"C:\Games\GTAVEnhanced\scripts\BongScript.wav";
                        try
                        {
                            if (File.Exists(soundPath))
                            {
                                _soundPlayer = new SoundPlayer(soundPath);
                                _soundPlayer.Play();
                            }
                            else
                            {
                                ModLogger.Log("BONG", $"Sound file not found: {soundPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Log("BONG", $"Failed to play sound: {ex.Message}");
                        }

                        _state = BongState.Smoking;
                        _stateTimer = Game.GameTime + 8300; // 600ms + 7700ms from original script
                    }
                    break;

                case BongState.Smoking:
                    if (Game.GameTime >= _stateTimer)
                    {
                        ModLogger.Log("BONG", "Smoking complete, releasing smoke and starting drug effect");
                        
                        // Выпускаем дым
                        Function.Call(Hash.USE_PARTICLE_FX_ASSET, "core");
                        Function.Call(Hash.START_PARTICLE_FX_NON_LOOPED_ON_PED_BONE, 
                            "ent_anim_cig_smoke", 
                            player.Handle, 
                            0.03f, 0.0f, 0.03f, 
                            0.0f, 0.0f, 0.0f, 
                            31086, // Head
                            2.4f, false, false, false);

                        // Удаляем пропы из рук
                        CleanupProps();

                        // Начинаем загружать походку пьяного
                        Function.Call(Hash.REQUEST_CLIP_SET, DrunkClipset);

                        _state = BongState.LoadingDrunkClipset;
                        _stateTimer = Game.GameTime + 1500;
                    }
                    break;

                case BongState.LoadingDrunkClipset:
                    if (Game.GameTime >= _stateTimer)
                    {
                        if (Function.Call<bool>(Hash.HAS_CLIP_SET_LOADED, DrunkClipset))
                        {
                            ModLogger.Log("BONG", "Drunk clipset loaded, applying drunk effects");
                            
                            Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, player.Handle, DrunkClipset, 1.0f);
                            Function.Call(Hash.SET_FACIAL_CLIPSET, player.Handle, FacialMood, null);
                            Function.Call(Hash.ANIMPOSTFX_PLAY, ScreenEffect, 0, true);
                            Function.Call(Hash.STOP_ANIM_TASK, player.Handle, AnimDict, AnimName, 3.0f);

                            _state = BongState.Drunk;
                            _stateTimer = Game.GameTime + 60000; // 60 seconds
                        }
                    }
                    break;

                case BongState.Drunk:
                    if (Game.GameTime >= _stateTimer)
                    {
                        _state = BongState.Cleanup;
                    }
                    break;

                case BongState.Cleanup:
                    ModLogger.Log("BONG", "Cleaning up drunk effects");
                    CleanupEffects(player);
                    _state = BongState.Idle;
                    break;
            }
        }

        public void Abort()
        {
            CleanupProps();
            Ped player = Game.Player.Character;
            if (player != null && player.Exists())
            {
                CleanupEffects(player);
            }
            _state = BongState.Idle;
        }

        private void CleanupProps()
        {
            if (_bongProp != null && _bongProp.Exists())
            {
                _bongProp.Delete();
            }
            _bongProp = null;

            if (_lighterProp != null && _lighterProp.Exists())
            {
                _lighterProp.Delete();
            }
            _lighterProp = null;

            if (_bongModel != null)
            {
                _bongModel.MarkAsNoLongerNeeded();
            }
            if (_lighterModel != null)
            {
                _lighterModel.MarkAsNoLongerNeeded();
            }

            if (_soundPlayer != null)
            {
                try
                {
                    _soundPlayer.Stop();
                    _soundPlayer.Dispose();
                }
                catch (Exception ex)
                {
                    ModLogger.Log("BONG", $"Failed to stop/dispose sound: {ex.Message}");
                }
                _soundPlayer = null;
            }
        }

        private void CleanupEffects(Ped player)
        {
            Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, player.Handle, 0.0f);
            Function.Call((Hash)8242245702733469743L, player.Handle);
            Function.Call(Hash.ANIMPOSTFX_STOP, ScreenEffect);
            Function.Call(Hash.RESET_PED_STRAFE_CLIPSET, player.Handle);
        }
    }
}
