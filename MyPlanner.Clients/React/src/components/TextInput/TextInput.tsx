import React from "react";

interface TextInputProps{
    styleName?: string | undefined,
    placeholderText?: string | undefined,
    value?: string | undefined,
    clearTextOnSubmit?: boolean | undefined,
    useTextArea?: boolean | undefined,
    onSubmit: (text: string) => void,
}

const TextInput: React.FC<TextInputProps> = (props)=>{
    const [currentText, setCurrentText] = React.useState<string>(props.value !== undefined ? props.value : "");
    const style: string = props.styleName !== undefined ? props.styleName : "p-1 rounded bg-gray-100 focus:bg-white focus:border-blue-500 focus:border";
    const clearTextOnSubmit: boolean = props.clearTextOnSubmit !== undefined ? props.clearTextOnSubmit : false;
    const useTextArea: boolean = props.useTextArea !== undefined ? props.useTextArea : false;

    React.useEffect(() => {
        setCurrentText(props.value !== undefined ? props.value : "");
    },
    [props.value]);

    const submitText = () => {
        if(currentText.length > 0 && currentText !== props.value){
            props.onSubmit(currentText);
            if(clearTextOnSubmit){
                setCurrentText("");
            }
        }
    }

    const onChange = (e: React.ChangeEvent<HTMLInputElement> | React.ChangeEvent<HTMLTextAreaElement>) =>{
        setCurrentText(e.target.value);
    }
    const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement> | React.KeyboardEvent<HTMLTextAreaElement>) => {
        if (e.key !== "Enter" && e.key !== "NumpadEnter")
            return;

        submitText();
    }

    if(useTextArea){
        return (
            <textarea value={currentText}
                    placeholder={props.placeholderText}
                    className={style}
                    onChange={onChange}
                    onBlur={submitText}
                    onKeyDown={onKeyDown}/>
        )
    }

    return (
        <input type="text" 
                value={currentText}
                placeholder={props.placeholderText}
                className={style}
                onChange={onChange}
                onBlur={submitText}
                onKeyDown={onKeyDown}/>
    )
}

export default TextInput;
