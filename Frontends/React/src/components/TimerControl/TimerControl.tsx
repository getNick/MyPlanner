import { faPlay, faStop } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import React, { useState, useEffect } from "react";

interface TimerControlProps {
    isRunning?: boolean;
    startTimestamp?: number;
    onStart?: (timestamp: number) => void;
    onStop?: (timestamp: number) => void;
}

const TimerControl: React.FC<TimerControlProps> = (props) => {
    const [time, setTime] = useState<number>(0);
    const [isRunning, setIsRunning] = useState<boolean>(props.isRunning || false);
    let startTimestamp: number | null = props.startTimestamp || null;

    useEffect(() => {
        let interval: NodeJS.Timeout | null = null;
        if (isRunning) {
            interval = setInterval(() => {
                if (startTimestamp === null) {
                    startTimestamp = Math.floor(Date.now() / 1000);
                }
                const currentTime = Math.floor(Date.now() / 1000);
                const elapsedTime = currentTime - startTimestamp;
                setTime(elapsedTime);
            }, 1000);
        } else if (interval) {
            clearInterval(interval);
        }
        return () => {
            if (interval) {
                clearInterval(interval);
            }
        };
    }, [isRunning]);

    const runTimer = () => {
        startTimestamp = Math.floor(Date.now() / 1000);
        setIsRunning(true);
        if (props.onStart) {
            props.onStart(startTimestamp);
        }
    };

    const stopTimer = () => {
        setIsRunning(false);
        if (props.onStop) {
            const currentTime = Math.floor(Date.now() / 1000);
            props.onStop(currentTime);
        }
    };

    const formatTime = (seconds: number): string => {
        const hrs = Math.floor(seconds / 3600)
            .toString();
        const mins = Math.floor((seconds % 3600) / 60)
            .toString();
        const secs = (seconds % 60).toString().padStart(2, "0");
        if (hrs === "0") {
            return `${mins}:${secs}`;
        }
        return `${hrs}:${mins.padStart(2, "0")}:${secs}`;
    };

    return (
        <div className="flex items-center space-x-4">
            {isRunning &&
                <div className="relative flex items-center justify-center h-6 text-red-400 font-mono text-lg">
                    <span className="relative">{formatTime(time)}</span>
                </div>}

            <button className="relative flex items-center justify-center h-6 w-6 bg-red-200 rounded-full transition duration-300 hover:bg-red-300"
                onClick={isRunning ? () => stopTimer : () => runTimer}>
                <FontAwesomeIcon icon={isRunning ? faStop : faPlay}
                    className="w-3 h-3 items-center justify-center text-red-500" />
            </button>
        </div>
    );
};

export default TimerControl;
