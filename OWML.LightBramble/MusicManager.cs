using OWML.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace OWML.LightBramble
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
			if (musicType == BrambleMusic.Spooky)
				clipToPlay = spookyClip;
			else
				clipToPlay = dekuClip;

			if (brambleOWAudioSource.clip == clipToPlay)	//if already playing that clip, then return
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
		Spooky,
		Deku
	}
}
