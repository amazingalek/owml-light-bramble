#define DEBUG

using OWML.Utils;
using UnityEngine;

namespace LightBramble
{
	public class MusicManager
	{
		public OWAudioSource brambleOWAudioSource;

		AudioClip spookyClip;
		AudioClip dekuClip;

		public MusicManager(GlobalMusicController globalMusicController)
		{
			brambleOWAudioSource = globalMusicController.GetValue<OWAudioSource>("_darkBrambleSource");

			spookyClip = brambleOWAudioSource.clip;
			dekuClip = LightBramble.inst.ModHelper.Assets.GetAudio("deku-tree.mp3");

			brambleOWAudioSource.SetValue("_clipArrayLength", 0);	//do this so that SelectClip returns early
			brambleOWAudioSource.SetTrack(OWAudioMixer.TrackName.Music);
		}

		public void SwapMusic(BrambleMusic musicType, float fadeInDuration = 1f, float fadeOutDuration = 1f)
		{
			AudioClip clipToPlay;
			if (musicType == BrambleMusic.Original)
				clipToPlay = spookyClip;
			else
				clipToPlay = dekuClip;

			LightBramble.inst.DebugLog("clipToPlay is " + (clipToPlay ? "not null" : "null"));
			if (brambleOWAudioSource == null || clipToPlay == null || brambleOWAudioSource.clip == clipToPlay)	//if already playing the same clip
				return;

			brambleOWAudioSource.clip = clipToPlay;
		}

		public void FadeOutMusic(float fadeOutDuration = 1f)
		{
			if (brambleOWAudioSource.IsFadingOut())
				return;
			brambleOWAudioSource.FadeOut(fadeOutDuration, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
		}
	}

	public enum BrambleMusic
	{
		Original,
		Deku
	}
}
