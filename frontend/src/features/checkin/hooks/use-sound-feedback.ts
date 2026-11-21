import { useRef } from "react";
import type { CheckInStatus } from "../../../api/types";

export function useSoundFeedback() {
  const sound = useRef<AudioContext | null>(null);

  const initAudio = async () => {
    sound.current = new AudioContext();
    await sound.current.resume();
  };

  const closeAudio = () => {
    if (sound.current && sound.current.state !== "closed") {
      void sound.current.close();
    }
  };

  const playTone = (status: CheckInStatus) => {
    try {
      const activeContext = sound.current ?? new AudioContext();
      const oscillator = activeContext.createOscillator();
      const gain = activeContext.createGain();

      oscillator.connect(gain);
      gain.connect(activeContext.destination);

      oscillator.frequency.value =
        status === "valid" ? 880 : status === "used" || status === "cancelled" ? 220 : 440;
      gain.gain.value = 0.08;

      oscillator.start();
      oscillator.stop(activeContext.currentTime + 0.13);

      if (!sound.current) {
        oscillator.onended = () => void activeContext.close();
      }
    } catch {
      // Audio is a progressive enhancement; visual feedback remains available.
    }
  };

  return { initAudio, closeAudio, playTone };
}

