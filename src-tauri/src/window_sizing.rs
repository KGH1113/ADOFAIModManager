use std::sync::Mutex;
use tauri::{LogicalSize, PhysicalPosition, WebviewWindow, WindowEvent};

const MIN: (f64, f64) = (900.0, 560.0);
const DEFAULT: (f64, f64) = (1120.0, 780.0);
const MAX: (f64, f64) = (1240.0, 860.0);
const MARGIN: f64 = 12.0;

#[derive(Debug)]
struct Sizing {
    min: LogicalSize<f64>,
    initial: LogicalSize<f64>,
    max: LogicalSize<f64>,
    zoom: f64,
}

fn sizing(work: (f64, f64), frame: (f64, f64), scale: f64) -> Sizing {
    let available = (
        ((work.0 - frame.0) / scale - MARGIN * 2.0).floor().max(1.0),
        ((work.1 - frame.1) / scale - MARGIN * 2.0).floor().max(1.0),
    );
    // Only displays smaller than the supported layout use uniform UI scaling.
    // Relaxing the native minimum alone would clip the content on high-DPI screens.
    let zoom = (available.0 / MIN.0).min(available.1 / MIN.1).min(1.0);
    let fit = |size: (f64, f64)| {
        LogicalSize::new(
            (size.0 * zoom).min(available.0),
            (size.1 * zoom).min(available.1),
        )
    };
    Sizing {
        min: fit(MIN),
        initial: fit(DEFAULT),
        max: fit(MAX),
        zoom,
    }
}

fn fit(window: &WebviewWindow, initial: bool) -> tauri::Result<()> {
    let Some(monitor) = window.current_monitor()?.or(window.primary_monitor()?) else {
        return Ok(());
    };
    let scale = window.scale_factor()?;
    let inner = window.inner_size()?;
    let outer = window.outer_size()?;
    let frame = (
        outer.width.saturating_sub(inner.width) as f64,
        outer.height.saturating_sub(inner.height) as f64,
    );
    let work = monitor.work_area();
    let plan = sizing(
        (work.size.width as f64, work.size.height as f64),
        frame,
        scale,
    );
    // Clear the old monitor's constraints before applying the new bounds.
    window.set_min_size(None::<LogicalSize<f64>>)?;
    window.set_max_size(None::<LogicalSize<f64>>)?;
    window.set_zoom(plan.zoom)?;
    let current = inner.to_logical::<f64>(scale);
    let target = if initial {
        plan.initial
    } else {
        LogicalSize::new(
            current.width.clamp(plan.min.width, plan.max.width),
            current.height.clamp(plan.min.height, plan.max.height),
        )
    };
    window.set_size(target)?;
    window.set_min_size(Some(plan.min))?;
    window.set_max_size(Some(plan.max))?;

    let width = (target.width * scale + frame.0).ceil() as i32;
    let height = (target.height * scale + frame.1).ceil() as i32;
    let left = work.position.x;
    let top = work.position.y;
    let right = left + (work.size.width as i32 - width).max(0);
    let bottom = top + (work.size.height as i32 - height).max(0);
    let position = window.outer_position()?;
    window.set_position(PhysicalPosition::new(
        if initial {
            left + (right - left) / 2
        } else {
            position.x.clamp(left, right)
        },
        if initial {
            top + (bottom - top) / 2
        } else {
            position.y.clamp(top, bottom)
        },
    ))?;
    Ok(())
}

pub fn initialize(window: &WebviewWindow) -> tauri::Result<()> {
    if let Err(error) = fit(window, true) {
        eprintln!("Unable to fit initial window to display: {error}");
    }
    // Start hidden so the oversized default window never flashes on small screens.
    window.show()?;

    let observed = window.clone();
    let monitor_key = || -> Option<_> {
        let monitor = window.current_monitor().ok()??;
        let work = monitor.work_area();
        Some((
            work.position,
            work.size,
            monitor.scale_factor(),
            window.scale_factor().ok()?,
        ))
    };
    let last_monitor = Mutex::new(monitor_key());
    window.on_window_event(move |event| {
        if !matches!(
            event,
            WindowEvent::Moved(_) | WindowEvent::ScaleFactorChanged { .. }
        ) {
            return;
        }
        let Ok(Some(monitor)) = observed.current_monitor() else {
            return;
        };
        let work = monitor.work_area();
        let Ok(scale) = observed.scale_factor() else {
            return;
        };
        let key = Some((work.position, work.size, monitor.scale_factor(), scale));
        let changed = {
            let mut previous = last_monitor.lock().unwrap();
            if *previous == key {
                false
            } else {
                *previous = key;
                true
            }
        };
        if changed {
            // Moving to another display must not leave the window off-screen.
            if let Err(error) = fit(&observed, false) {
                eprintln!("Unable to fit window to display: {error}");
            }
        }
    });
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn normal_display_keeps_default_and_tight_limits() {
        let plan = sizing((1920.0, 1040.0), (16.0, 39.0), 1.0);
        assert_eq!(plan.initial, LogicalSize::new(DEFAULT.0, DEFAULT.1));
        assert_eq!(plan.min, LogicalSize::new(MIN.0, MIN.1));
        assert_eq!(plan.max, LogicalSize::new(MAX.0, MAX.1));
        assert_eq!(plan.zoom, 1.0);
    }

    #[test]
    fn low_resolution_excludes_taskbar_frame_and_margin() {
        let plan = sizing((1366.0, 728.0), (16.0, 39.0), 1.0);
        assert_eq!(plan.initial, LogicalSize::new(1120.0, 665.0));
        assert_eq!(plan.max.height, 665.0);
        assert_eq!(plan.zoom, 1.0);
    }

    #[test]
    fn high_dpi_and_tiny_displays_keep_layout_proportional() {
        for (work, frame, scale) in [
            ((1366.0, 728.0), (20.0, 49.0), 1.25),
            ((1920.0, 1000.0), (32.0, 78.0), 2.0),
            ((800.0, 560.0), (16.0, 39.0), 1.0),
        ] {
            let plan = sizing(work, frame, scale);
            assert!(plan.zoom < 1.0);
            assert!((plan.min.width / plan.zoom - MIN.0).abs() < 0.001);
            assert!((plan.min.height / plan.zoom - MIN.1).abs() < 0.001);
            assert!(plan.min.width <= plan.initial.width && plan.initial.width <= plan.max.width);
            assert!(
                plan.min.height <= plan.initial.height && plan.initial.height <= plan.max.height
            );
            assert!(plan.max.width * scale + frame.0 <= work.0);
            assert!(plan.max.height * scale + frame.1 <= work.1);
        }
    }
}
