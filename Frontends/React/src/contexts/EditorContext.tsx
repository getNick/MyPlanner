import React, { createContext, useRef, useState } from "react";
import EditorJS, { OutputData } from "@editorjs/editorjs";
// @ts-ignore
import List from "@editorjs/list";
// @ts-ignore
import Checklist from '@editorjs/checklist'
// @ts-ignore
import YoutubeEmbed from 'editorjs-youtube-embed';
// @ts-ignore
import Header from 'editorjs-header-with-alignment';

export type EditorContextType = {
    initEditor: () => void;
    editorInstanceRef: React.MutableRefObject<EditorJS | null>;
}


export const EditorContext = React.createContext<EditorContextType | null>(null);

const EditorContextProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const editorInstanceRef = useRef<EditorJS | null>(null);
    const initEditor = () => {
        if (!editorInstanceRef.current) {
            const editor = new EditorJS({
                holder: "editorjs",
                placeholder: "Let's take a note!",
                tools: {
                    header: {
                        class: Header,
                        config: {
                            placeholder: 'Enter a header',
                            levels: [2, 3, 4],
                            defaultLevel: 3,
                            defaultAlignment: 'left'
                        }
                    },
                    List,
                    Checklist,
                    youtubeEmbed: YoutubeEmbed,
                }
            })
            editorInstanceRef.current = editor;
        }
    }

    return (
        <EditorContext.Provider value={{ initEditor, editorInstanceRef }}>
            {children}
        </EditorContext.Provider>
    );
}

export default EditorContextProvider;

export const useEditorContext = () => {
    const context = React.useContext(EditorContext);
    if (!context) {
        throw new Error('useEditorContext must be used within a EditorContextProvider');
    }
    return context;
};