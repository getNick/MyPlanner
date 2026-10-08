import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import BillImageViewer from "./BillImageViewer";

describe("BillImageViewer hover magnification", () => {
  function movePointerOverImage() {
    const image = screen.getByAltText("Original Bill Image");
    jest.spyOn(image, "getBoundingClientRect").mockReturnValue({
      left: 0, top: 0, right: 100, bottom: 200, width: 100, height: 200, x: 0, y: 0, toJSON: () => ({}),
    } as DOMRect);
    fireEvent.pointerMove(screen.getByLabelText(/Bill Image preview/), { clientX: 50, clientY: 100 });
  }

  it("uses a more modest default hover magnification", () => {
    render(<BillImageViewer src="bill.jpg" alt="Original Bill Image" />);
    movePointerOverImage();

    expect(screen.getByLabelText(/Bill Image preview/).querySelector("div")?.getAttribute("style")).toContain("background-size: 150% auto");
  });

  it("adjusts hover magnification with the mouse wheel and keeps it within limits", () => {
    render(<BillImageViewer src="bill.jpg" alt="Original Bill Image" />);
    const preview = screen.getByLabelText(/Bill Image preview/);
    movePointerOverImage();

    expect(fireEvent.wheel(preview, { deltaY: -100, cancelable: true })).toBe(false);
    expect(preview.querySelector("div")?.getAttribute("style")).toContain("background-size: 175% auto");

    fireEvent.wheel(preview, { deltaY: 100 });
    expect(preview.querySelector("div")?.getAttribute("style")).toContain("background-size: 150% auto");

    for (let index = 0; index < 20; index += 1) fireEvent.wheel(preview, { deltaY: 100 });
    expect(preview.querySelector("div")?.getAttribute("style")).toContain("background-size: 100% auto");
    fireEvent.wheel(preview, { deltaY: 100 });
    expect(preview.querySelector("div")?.getAttribute("style")).toContain("background-size: 100% auto");
  });
});
