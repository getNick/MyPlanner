import React, { useEffect, useRef, useState } from "react";
import TextInput from "../../components/TextInput/TextInput";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronLeft } from "@fortawesome/free-solid-svg-icons";
import { useTodoContext } from "../../contexts/TodoContext";
import { useLoaderData, useNavigate } from "react-router-dom";

import EditorJS, { OutputData } from "@editorjs/editorjs";
// @ts-ignore
import List from "@editorjs/list";
// @ts-ignore
import Checklist from '@editorjs/checklist'
// @ts-ignore
import YoutubeEmbed from 'editorjs-youtube-embed';
// @ts-ignore
import Header from 'editorjs-header-with-alignment';

import UpdatePage from "../../entities/Pages/UpdatePage";
import UpdateNote from "../../entities/Note/UpdateNote";
import Note from "../../entities/Note/Note";


const NotePage: React.FC = () => {
    const pageId = useLoaderData() as string;
    const navigate = useNavigate();
    const { todoService, getCachedTitle } = useTodoContext();
    const [title, setTitle] = useState<string | undefined>();
    const [note, setNote] = useState<Note | undefined>();

    const editorInstanceRef = useRef<EditorJS | null>(null);

    const fetchContent = async () => {
        setTitle(getCachedTitle(pageId));
        const note = await todoService.getNote(pageId);
        setNote(note);
        if (note?.content !== undefined && note?.content !== "") {
            editorInstanceRef.current?.render(JSON.parse(note?.content))
        }
    }

    useEffect(() => {
        initEditor();
        fetchContent();
    }, []);

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

    const onTitleChanged = async (newTitle: string) => {
        let changeTitleChange: UpdatePage = new UpdatePage(pageId);
        changeTitleChange.title = newTitle;
        await todoService.updateFolder(changeTitleChange);
    }
    const onContentChanged = async () => {
        if (note?.id === undefined)
            return;
        const descriptionData: OutputData | undefined = await editorInstanceRef.current?.save();
        let updateNote: UpdateNote = new UpdateNote(note?.id);
        updateNote.content = JSON.stringify(descriptionData);;
        await todoService.updateNote(updateNote);
    }

    const navigateBack = () => {
        navigate(-1);
    }

    return (
        <div className="flex flex-col h-full w-full m-1">
            <div className="flex h-10 items-center">
                <button className="h-8 w-8 rounded hover:bg-slate-200" onClick={navigateBack}>
                    <FontAwesomeIcon icon={faChevronLeft} />
                </button>

                <TextInput styleName="w-full h-10 p-1 font-bold text-xl"
                    onSubmit={onTitleChanged}
                    placeholderText="Title"
                    value={title} />
            </div>

            <div id="editorjs" onBlur={onContentChanged}></div>
        </div>
    )
}

export default NotePage;